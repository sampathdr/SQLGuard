# SqlGuard CI/CD Integration Examples

## GitHub Actions

```yaml
# .github/workflows/db-security.yml
name: Database Security Scan

on:
  schedule:
    - cron: '0 6 * * 1'        # Every Monday at 06:00 UTC
  push:
    branches: [ main ]

jobs:
  scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Install SqlGuard
        run: dotnet tool install -g SqlGuard.Cli

      # Mandatory rules only — blocks the pipeline on failure
      - name: Scan Production DB (mandatory rules)
        run: |
          sqlguard scan \
            --engine SqlServer \
            --host ${{ secrets.PROD_DB_HOST }} \
            --database ${{ secrets.PROD_DB_NAME }} \
            --user sqlguard_scanner \
            --password-env SQLGUARD_PASS \
            --packs sqlserver-core,sqlserver-cis \
            --fail-on mandatory \
            --format json \
            --output scan-results.json
        env:
          SQLGUARD_PASS: ${{ secrets.PROD_DB_PASSWORD }}

      # Advisory scan — warns but never fails the pipeline
      - name: Advisory Scan (all rules)
        run: |
          sqlguard scan \
            --engine SqlServer \
            --host ${{ secrets.PROD_DB_HOST }} \
            --database ${{ secrets.PROD_DB_NAME }} \
            --user sqlguard_scanner \
            --password-env SQLGUARD_PASS \
            --packs sqlserver-core,sqlserver-cis,community \
            --fail-on critical \
            --format html \
            --output advisory-report.html
        env:
          SQLGUARD_PASS: ${{ secrets.PROD_DB_PASSWORD }}
        continue-on-error: true   # advisory only

      - name: Upload reports
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: sqlguard-reports
          path: |
            scan-results.json
            advisory-report.html
```

## Azure DevOps Pipeline

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include: [ main ]

pool:
  vmImage: ubuntu-latest

steps:
  - task: DotNetCoreCLI@2
    displayName: Install SqlGuard
    inputs:
      command: custom
      custom: tool
      arguments: install -g SqlGuard.Cli

  - script: |
      sqlguard scan \
        --engine PostgreSQL \
        --host $(DB_HOST) \
        --database $(DB_NAME) \
        --user $(DB_USER) \
        --password-env DB_PASSWORD \
        --packs postgresql-core \
        --fail-on mandatory \
        --format json \
        --output $(Build.ArtifactStagingDirectory)/scan.json
    displayName: SqlGuard Security Scan
    env:
      DB_PASSWORD: $(DB_PASSWORD)

  - task: PublishBuildArtifacts@1
    inputs:
      pathToPublish: $(Build.ArtifactStagingDirectory)
      artifactName: sqlguard-report
```

## Minimum-permission scanner account (SQL Server)

```sql
-- Create a dedicated read-only scanner login
CREATE LOGIN [sqlguard_scanner] WITH PASSWORD = '<strong_password>',
    CHECK_POLICY = ON, CHECK_EXPIRATION = ON;

CREATE USER [sqlguard_scanner] FOR LOGIN [sqlguard_scanner];

-- Grant only the permissions needed for scanning
GRANT VIEW SERVER STATE TO [sqlguard_scanner];
GRANT VIEW ANY DEFINITION TO [sqlguard_scanner];
GRANT VIEW ANY DATABASE TO [sqlguard_scanner];
-- Required for sys.dm_server_registry
GRANT VIEW SERVER PERFORMANCE STATE TO [sqlguard_scanner];
```

## Minimum-permission scanner account (PostgreSQL)

```sql
CREATE ROLE sqlguard_scanner WITH LOGIN PASSWORD '<strong_password>';
GRANT pg_monitor TO sqlguard_scanner;
GRANT SELECT ON pg_hba_file_rules TO sqlguard_scanner;
```