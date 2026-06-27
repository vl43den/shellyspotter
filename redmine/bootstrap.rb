# frozen_string_literal: true
#
# Idempotent Redmine bootstrap — run once on every `docker compose up` so a FRESH
# host gets a working ticket integration with no manual clicking. Executed by the
# one-shot `redmine-bootstrap` service via `bundle exec rails runner`.
#
# It makes the on-prem Redmine match what Core's TicketService expects:
#   * default configuration data (trackers, statuses, priorities, workflows, roles)
#   * REST API enabled
#   * admin password set + forced-change cleared (so the UI is usable for the demo)
#   * admin API key pinned to REDMINE_API_KEY (so the key baked into .env always matches)
#   * a private project with identifier "shellyspotter" that has all trackers enabled
#
# Re-running is safe: every step checks current state before changing anything.

log = ->(msg) { puts "[redmine-bootstrap] #{msg}" }

# 1) Wait until the main redmine container has finished `db:migrate` (schema present).
ready = false
30.times do
  begin
    ActiveRecord::Base.connection.execute('SELECT 1 FROM settings LIMIT 1')
    ready = true
    break
  rescue StandardError => e
    log.call("waiting for redmine schema (#{e.class})...")
    sleep 5
  end
end
abort('[redmine-bootstrap] schema never became ready — aborting') unless ready

# 2) Load Redmine's default data if this is a fresh database (no trackers/etc.).
if Redmine::DefaultData::Loader.no_data?
  Redmine::DefaultData::Loader.load('en')
  log.call('default configuration data loaded')
else
  log.call('default data already present — skipping')
end

# 3) Enable the REST API (Core talks to Redmine over REST).
if Setting.rest_api_enabled?
  log.call('REST API already enabled')
else
  Setting.rest_api_enabled = '1'
  log.call('REST API enabled')
end

admin = User.find_by_login('admin')

# 4) Make the admin account demo-ready: set password, clear the forced password change.
if admin
  pw = ENV['SS_ADMIN_PASSWORD'].to_s
  unless pw.empty?
    admin.password = pw
    admin.password_confirmation = pw
  end
  admin.must_change_passwd = false
  admin.save!(validate: false)
  log.call('admin account updated (password set, forced-change cleared)')
end

# 5) Pin the admin's API key to the value Core already has in .env, so the
#    integration works without copy-pasting a freshly generated key.
key = ENV['SS_API_KEY'].to_s
if admin && !key.empty?
  # find_or_create_by persists the row (with a random value from Token's
  # before_create callback); update_column then writes our value directly,
  # bypassing that callback (a plain save! would regenerate it).
  token = Token.find_or_create_by(user_id: admin.id, action: 'api')
  if token.value == key
    log.call('admin API key already matches provided value')
  else
    token.update_column(:value, key)
    log.call('admin API key pinned to provided REDMINE_API_KEY')
  end
elsif key.empty?
  log.call('WARNING: SS_API_KEY is empty — Core will not be able to authenticate')
end

# 6) Ensure the "shellyspotter" project exists with issue tracking + all trackers.
project = Project.find_by_identifier('shellyspotter')
if project
  log.call("project 'shellyspotter' already exists")
else
  project = Project.new(name: 'ShellySpotter', identifier: 'shellyspotter', is_public: false)
  project.enabled_module_names = ['issue_tracking']
  project.trackers = Tracker.all
  project.save!
  log.call("project 'shellyspotter' created with trackers: #{Tracker.pluck(:name).join(', ')}")
end

log.call('done.')
