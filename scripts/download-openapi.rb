#!/usr/bin/env ruby
# frozen_string_literal: true

require 'net/http'
require 'openssl'
require 'uri'

module OpenApiDownload
  MAX_BYTES = 8 * 1024 * 1024
  MAX_REDIRECTS = 3

  class Error < StandardError; end

  module_function

  def fetch(uri, max_bytes: MAX_BYTES, redirects: MAX_REDIRECTS, original_origin: nil,
            http_factory: method(:build_http))
    validate_uri!(uri)
    original_origin ||= origin(uri)
    raise Error, 'OpenAPI redirect limit exceeded' if redirects.negative?

    response, body = request(uri, max_bytes, http_factory)
    status = response.code.to_i
    return body if status.between?(200, 299)
    raise Error, "OpenAPI source returned HTTP #{response.code}" unless status.between?(300, 399)

    location = response['location']
    raise Error, 'OpenAPI redirect did not include Location' if location.nil? || location.empty?

    redirected = URI.join(uri, location)
    validate_uri!(redirected)
    raise Error, 'OpenAPI redirects must remain on the same origin' unless origin(redirected) == original_origin

    fetch(redirected, max_bytes: max_bytes, redirects: redirects - 1,
          original_origin: original_origin, http_factory: http_factory)
  rescue URI::InvalidURIError
    raise Error, 'OpenAPI source or redirect is not a valid HTTPS URL'
  end

  def request(uri, max_bytes, http_factory)
    request = Net::HTTP::Get.new(uri.request_uri)
    request['Accept-Encoding'] = 'identity'
    request['User-Agent'] = 'viapost-dotnet-contract-check/0.2'
    response = nil
    body = String.new(encoding: Encoding::BINARY)

    http_factory.call(uri).start do |session|
      session.request(request) do |raw_response|
        response = raw_response
        next unless raw_response.code.to_i.between?(200, 299)

        content_length = Integer(raw_response['content-length'], exception: false)
        raise Error, "OpenAPI source exceeds #{max_bytes} bytes" if content_length && content_length > max_bytes

        raw_response.read_body do |chunk|
          raise Error, "OpenAPI source exceeds #{max_bytes} bytes" if body.bytesize + chunk.bytesize > max_bytes

          body << chunk
        end
      end
    end
    [response, body]
  end

  def build_http(uri)
    http = Net::HTTP.new(uri.host, uri.port)
    http.use_ssl = true
    http.verify_mode = OpenSSL::SSL::VERIFY_PEER
    http.open_timeout = 5
    http.read_timeout = 15
    http.write_timeout = 15 if http.respond_to?(:write_timeout=)
    http
  end

  def validate_uri!(uri)
    valid = uri.is_a?(URI::HTTPS) && uri.host && !uri.userinfo && !uri.fragment
    raise Error, 'OpenAPI source and redirects must use an unambiguous HTTPS URL' unless valid
  end

  def origin(uri)
    [uri.scheme.downcase, uri.hostname.downcase, uri.port]
  end

  def run(source, destination)
    File.binwrite(destination, fetch(URI(source)))
  rescue Error => e
    abort e.message
  end
end

OpenApiDownload.run(ARGV.fetch(0), ARGV.fetch(1)) if $PROGRAM_NAME == __FILE__
