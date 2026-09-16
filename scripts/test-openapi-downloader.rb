#!/usr/bin/env ruby
# frozen_string_literal: true

require_relative 'download-openapi'

class FakeResponse
  attr_reader :code

  def initialize(code, body = '', headers = {})
    @code = code.to_s
    @body = body
    @headers = headers.transform_keys(&:downcase)
  end

  def [](name)
    @headers[name.downcase]
  end

  def read_body
    yield(@body)
  end
end

class FakeHttp
  def initialize(response)
    @response = response
  end

  def start
    yield(self)
  end

  def request(_request)
    yield(@response)
  end
end

def factory(*responses)
  queue = responses.to_a
  lambda do |_uri|
    raise 'unexpected request' if queue.empty?

    FakeHttp.new(queue.shift)
  end
end

def rejects(label)
  yield
  abort "#{label} was accepted"
rescue OpenApiDownload::Error
  nil
end

source = URI('https://docs.viapost.io/openapi/public.yaml')
body = OpenApiDownload.fetch(
  source,
  http_factory: factory(FakeResponse.new(302, '', 'location' => '/canonical.yaml'), FakeResponse.new(200, 'openapi'))
)
abort 'same-origin redirect did not preserve body' unless body == 'openapi'

rejects('cross-origin redirect') do
  OpenApiDownload.fetch(source, http_factory: factory(FakeResponse.new(302, '', 'location' => 'https://evil.test/openapi.yaml')))
end
rejects('HTTP downgrade') do
  OpenApiDownload.fetch(source, http_factory: factory(FakeResponse.new(302, '', 'location' => 'http://docs.viapost.io/openapi.yaml')))
end
rejects('oversized body') do
  OpenApiDownload.fetch(source, max_bytes: 4, http_factory: factory(FakeResponse.new(200, '12345')))
end
rejects('redirect loop') do
  redirects = Array.new(OpenApiDownload::MAX_REDIRECTS + 2) { FakeResponse.new(302, '', 'location' => '/again') }
  OpenApiDownload.fetch(source, http_factory: factory(*redirects))
end

puts 'OpenAPI downloader security tests passed'
