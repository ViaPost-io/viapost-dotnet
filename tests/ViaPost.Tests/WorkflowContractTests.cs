using Xunit;

namespace ViaPost.Tests;

public sealed class WorkflowContractTests
{
    [Fact]
    public void Release_is_created_only_after_verification_and_attestation()
    {
        var workflow = File.ReadAllText(RepoFile(".github/workflows/release.yml"));

        Assert.Contains("tags: ['v*']", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: [verify-and-build, attest-build-provenance]", workflow, StringComparison.Ordinal);
        Assert.Contains("subject-path: artifacts/*", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release create \"$RELEASE_TAG\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("types: [published]", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--clobber", workflow, StringComparison.Ordinal);
        Assert.Contains("if: github.event_name == 'push'", workflow, StringComparison.Ordinal);
        Assert.Contains("EVENT_SHA: ${{ github.sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("NUGET_PACKAGES: ${{ runner.temp }}/nuget-packages", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release download \"$RELEASE_TAG\"", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release view \"$RELEASE_TAG\" --json isDraft --jq .isDraft", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Release_retry_reuses_and_verifies_the_immutable_published_assets()
    {
        var workflow = File.ReadAllText(RepoFile(".github/workflows/release.yml"));

        Assert.DoesNotContain("cmp --silent", workflow, StringComparison.Ordinal);
        Assert.Contains("sha256sum -c SHA256SUMS", workflow, StringComparison.Ordinal);
        Assert.Contains("gh attestation verify \"$artifact\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--source-digest \"$SOURCE_SHA\"", workflow, StringComparison.Ordinal);
        Assert.Contains("--signer-workflow \"$GH_REPO/.github/workflows/release.yml\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_nuget_publish_uses_the_exact_attested_release_package()
    {
        var workflow = File.ReadAllText(RepoFile(".github/workflows/release.yml"));
        var job = workflow[(workflow.IndexOf("  publish-to-nuget:", StringComparison.Ordinal))..];

        Assert.Contains("gh release download \"$RELEASE_TAG\" --dir artifacts", job, StringComparison.Ordinal);
        Assert.Contains("test \"$(gh release view \"$RELEASE_TAG\" --json isDraft --jq .isDraft)\" = false", job, StringComparison.Ordinal);
        Assert.Contains("scripts/check-release-tag.sh \"$RELEASE_TAG\"", job, StringComparison.Ordinal);
        Assert.Contains("sha256sum -c SHA256SUMS", job, StringComparison.Ordinal);
        Assert.Contains("gh attestation verify \"$artifact\"", job, StringComparison.Ordinal);
        Assert.Contains("github.ref == format('refs/tags/{0}', inputs.tag)", job, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/download-artifact", job, StringComparison.Ordinal);
        Assert.DoesNotContain("--skip-duplicate", job, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet pack", job, StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_drift_uses_the_published_public_contract()
    {
        var workflow = File.ReadAllText(RepoFile(".github/workflows/contract-drift.yml"));

        Assert.Contains("https://docs.viapost.io/openapi/public.yaml", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("raw.githubusercontent.com/ViaPost-io/base-code", workflow, StringComparison.Ordinal);
    }

    private static string RepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".github")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
