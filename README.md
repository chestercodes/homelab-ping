# homelab-ping

Example app with tests, migrations and monitoring.

The app doesn't do anything particularly interesting it just receives a GET request on the `/ping` route and makes a request to a postgres database table.

## interesting features

- uses custom serlog text formatter to print out logs as loki expects. the standard text formatter doesn't have a new line after each log event
- exports metrics to prometheus with both stable and canary services
- uses argo rollout canary deployments and metrics to ensure canary doesn't break deployment
- deploys dashboard to grafana
- reports sync status to workflow reporter