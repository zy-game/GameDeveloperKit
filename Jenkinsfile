pipeline {
    agent { label 'unity-windows' }

    options {
        disableConcurrentBuilds()
        skipDefaultCheckout(true)
        timestamps()
    }

    parameters {
        string(name: 'CHANNEL', defaultValue: 'dev', description: 'Channel safe segment')
        choice(name: 'DEPLOY_ENVIRONMENT', choices: ['dev', 'test', 'staging', 'prod'], description: 'Deployment environment')
        string(name: 'FLAVOR', defaultValue: '', description: 'Optional flavor safe segment')
        string(name: 'BUILD_TARGET', defaultValue: 'Android', description: 'Exact Unity BuildTarget name')
        string(name: 'PLAYER_VERSION', defaultValue: '0.0.0-smoke', description: 'Smoke version safe segment')
        string(name: 'PLAYER_BUILD_NUMBER', defaultValue: '1', description: 'Positive build number independent from Jenkins BUILD_NUMBER')
        string(name: 'PROFILE', defaultValue: 'android-dev', description: 'Profile id in generated fixture catalog')
        booleanParam(name: 'RUN_PLAYER_BUILD', defaultValue: false, description: 'Run full responder/resource/Player build after quality gate')
    }

    environment {
        GDK_CHANNEL = "${params.CHANNEL}"
        GDK_ENVIRONMENT = "${params.DEPLOY_ENVIRONMENT}"
        GDK_FLAVOR = "${params.FLAVOR}"
        GDK_BUILD_TARGET = "${params.BUILD_TARGET}"
        GDK_PLAYER_VERSION = "${params.PLAYER_VERSION}"
        GDK_PLAYER_BUILD_NUMBER = "${params.PLAYER_BUILD_NUMBER}"
        GDK_PROFILE = "${params.PROFILE}"
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def scmVars = checkout scm
                    env.GDK_REVISION = scmVars.GIT_COMMIT
                    env.GDK_FIXTURE_ROOT = env.WORKSPACE_TMP ?: "${env.WORKSPACE}@tmp"
                    env.GDK_SMOKE_PROJECT = "${env.GDK_FIXTURE_ROOT}\\channel-build-smoke"
                    env.GDK_OUTPUT_ROOT = "${env.WORKSPACE}\\Build\\Channel"
                    env.GDK_REPORT_PATH = "${env.GDK_OUTPUT_ROOT}\\channel-build-report.json"
                    env.GDK_EDITOR_LOG = "${env.GDK_OUTPUT_ROOT}\\unity-editor.log"
                    env.GDK_QUALITY_PROJECT = "${env.GDK_FIXTURE_ROOT}\\channel-quality"
                    env.GDK_QUALITY_RESULTS = "${env.GDK_OUTPUT_ROOT}\\quality-editmode.xml"
                    env.GDK_QUALITY_LOG = "${env.GDK_OUTPUT_ROOT}\\quality-editmode.log"
                    if (!env.GDK_REVISION?.trim()) {
                        error('Checkout did not provide GIT_COMMIT.')
                    }
                }
            }
        }

        stage('Local Quality Gate') {
            steps {
                powershell '''
                    & pwsh -NoProfile -File "$env:WORKSPACE\Tools\CI\Jenkins\invoke-local-quality-gate.ps1" `
                        -UnityEditorPath $env:UNITY_EDITOR_PATH `
                        -ProjectPath $env:GDK_QUALITY_PROJECT `
                        -FixtureRoot $env:GDK_FIXTURE_ROOT `
                        -PackagePath $env:WORKSPACE `
                        -ResultsPath $env:GDK_QUALITY_RESULTS `
                        -LogPath $env:GDK_QUALITY_LOG
                    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                '''
                junit(testResults: 'Build/Channel/quality-editmode.xml', allowEmptyResults: false)
            }
        }

        stage('Prepare Smoke Fixture') {
            steps {
                powershell '''
                    $arguments = @(
                        '-NoProfile',
                        '-File', "$env:WORKSPACE\Tools\CI\Jenkins\New-ChannelBuildSmokeProject.ps1",
                        '-ProjectPath', $env:GDK_SMOKE_PROJECT,
                        '-FixtureRoot', $env:GDK_FIXTURE_ROOT,
                        '-PackagePath', $env:WORKSPACE,
                        '-UnityEditorPath', $env:UNITY_EDITOR_PATH,
                        '-Channel', $env:GDK_CHANNEL,
                        '-Profile', $env:GDK_PROFILE
                    )
                    if ([System.Convert]::ToBoolean($env:RUN_PLAYER_BUILD)) {
                        $arguments += '-IncludePlayerScene'
                    }
                    & pwsh @arguments
                    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                '''
            }
        }

        stage('Channel Build') {
            steps {
                script {
                    def exitCode = powershell(
                        returnStatus: true,
                        script: '''
                            $arguments = @(
                                '-NoProfile',
                                '-File', "$env:WORKSPACE\Tools\CI\Jenkins\invoke-channel-build.ps1",
                                '-UnityEditorPath', $env:UNITY_EDITOR_PATH,
                                '-ProjectPath', $env:GDK_SMOKE_PROJECT,
                                '-FixtureRoot', $env:GDK_FIXTURE_ROOT,
                                '-Channel', $env:GDK_CHANNEL,
                                '-Environment', $env:GDK_ENVIRONMENT,
                                '-BuildTarget', $env:GDK_BUILD_TARGET,
                                '-Version', $env:GDK_PLAYER_VERSION,
                                '-PlayerBuildNumber', $env:GDK_PLAYER_BUILD_NUMBER,
                                '-Profile', $env:GDK_PROFILE,
                                '-OutputRoot', $env:GDK_OUTPUT_ROOT,
                                '-ReportPath', $env:GDK_REPORT_PATH,
                                '-LogPath', $env:GDK_EDITOR_LOG,
                                '-Mode', $(if ([System.Convert]::ToBoolean($env:RUN_PLAYER_BUILD)) { 'player' } else { 'validate' }),
                                '-CiProvider', 'jenkins',
                                '-CiJobName', $env:JOB_NAME,
                                '-CiBuildId', $env:BUILD_NUMBER,
                                '-CiRevision', $env:GDK_REVISION
                            )
                            if (-not [string]::IsNullOrWhiteSpace($env:BUILD_URL)) {
                                $arguments += @('-CiBuildUrl', $env:BUILD_URL)
                            }
                            if (-not [string]::IsNullOrWhiteSpace($env:GDK_FLAVOR)) {
                                $arguments += @('-Flavor', $env:GDK_FLAVOR)
                            }
                            & pwsh @arguments
                            exit $LASTEXITCODE
                        ''')
                    env.GDK_CHANNEL_EXIT = exitCode.toString()
                }
            }
        }

        stage('Validate Report') {
            steps {
                powershell '''
                    & pwsh -NoProfile -File "$env:WORKSPACE\Tools\CI\Jenkins\test-channel-build-report.ps1" `
                        -ReportPath $env:GDK_REPORT_PATH `
                        -OutputRoot $env:GDK_OUTPUT_ROOT `
                        -ExpectedExitCode ([int]$env:GDK_CHANNEL_EXIT)
                    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                '''
                script {
                    if (env.GDK_CHANNEL_EXIT != '0') {
                        error("Unity channel command failed with exit code ${env.GDK_CHANNEL_EXIT}.")
                    }
                }
            }
        }
    }

    post {
        always {
            archiveArtifacts(
                artifacts: 'Build/Channel/channel-build-report.json,Build/Channel/unity-editor.log,Build/Channel/quality-editmode.xml,Build/Channel/quality-editmode.log,Build/Channel/player/**/*',
                allowEmptyArchive: true,
                fingerprint: true)
        }
    }
}
