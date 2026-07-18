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
        booleanParam(name: 'PUBLISH_RESOURCES', defaultValue: false, description: 'Stage immutable resources without changing current pointer')
        booleanParam(name: 'PROMOTE_RESOURCES', defaultValue: false, description: 'Approve and promote the staged resource release')
        string(name: 'MINIMUM_CLIENT_BUILD', defaultValue: '1', description: 'Minimum compatible client build when staging resources')
        string(name: 'MAXIMUM_CLIENT_BUILD', defaultValue: '1', description: 'Maximum compatible client build when staging resources')
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
                    env.GDK_FIXTURE_ROOT = env.GDK_UNITY_FIXTURE_ROOT?.trim() ?:
                        (env.WORKSPACE_TMP ?: "${env.WORKSPACE}@tmp")
                    env.GDK_SMOKE_PROJECT = "${env.GDK_FIXTURE_ROOT}\\channel-build-smoke"
                    env.GDK_OUTPUT_ROOT = "${env.WORKSPACE}\\Build\\Channel"
                    env.GDK_REPORT_PATH = "${env.GDK_OUTPUT_ROOT}\\channel-build-report.json"
                    env.GDK_EDITOR_LOG = "${env.GDK_OUTPUT_ROOT}\\unity-editor.log"
                    env.GDK_QUALITY_PROJECT = "${env.GDK_FIXTURE_ROOT}\\channel-quality"
                    env.GDK_QUALITY_RESULTS = "${env.GDK_OUTPUT_ROOT}\\quality-editmode.xml"
                    env.GDK_QUALITY_LOG = "${env.GDK_OUTPUT_ROOT}\\quality-editmode.log"
                    env.GDK_RELEASE_RESULT = "${env.GDK_OUTPUT_ROOT}\\staged-release.json"
                    env.GDK_PROMOTION_RESULT = "${env.GDK_OUTPUT_ROOT}\\promotion-result.json"
                    if (!env.GDK_REVISION?.trim()) {
                        error('Checkout did not provide GIT_COMMIT.')
                    }
                }
            }
        }

        stage('Local Quality Gate') {
            steps {
                powershell '''
                    & pwsh -NoProfile -File "$env:WORKSPACE/Tools/CI/Jenkins/invoke-local-quality-gate.ps1" `
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
                        '-File', "$env:WORKSPACE/Tools/CI/Jenkins/New-ChannelBuildSmokeProject.ps1",
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
                                '-File', "$env:WORKSPACE/Tools/CI/Jenkins/invoke-channel-build.ps1",
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
                    & pwsh -NoProfile -File "$env:WORKSPACE/Tools/CI/Jenkins/test-channel-build-report.ps1" `
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

        stage('Publish Immutable Resources') {
            when {
                expression { return params.PUBLISH_RESOURCES }
            }
            steps {
                script {
                    if (!params.RUN_PLAYER_BUILD) {
                        error('PUBLISH_RESOURCES requires RUN_PLAYER_BUILD.')
                    }
                    if (!env.GDK_COS_REGION?.trim() || !env.GDK_COS_BUCKET?.trim() ||
                        !env.GDK_COS_CREDENTIAL_ID?.trim()) {
                        error('COS publish job environment is incomplete.')
                    }
                    def minimum = params.MINIMUM_CLIENT_BUILD as Long
                    def maximum = params.MAXIMUM_CLIENT_BUILD as Long
                    if (minimum <= 0 || maximum < minimum) {
                        error('Resource client build range is invalid.')
                    }
                    withCredentials([usernamePassword(
                        credentialsId: env.GDK_COS_CREDENTIAL_ID,
                        usernameVariable: 'GDK_COS_SECRET_ID',
                        passwordVariable: 'GDK_COS_SECRET_KEY')]) {
                        powershell '''
                            & pwsh -NoProfile -File "$env:WORKSPACE/Tools/CI/Jenkins/invoke-resource-release.ps1" `
                                -ReportPath $env:GDK_REPORT_PATH `
                                -OutputRoot $env:GDK_OUTPUT_ROOT `
                                -MinimumClientBuild ([long]$env:MINIMUM_CLIENT_BUILD) `
                                -MaximumClientBuild ([long]$env:MAXIMUM_CLIENT_BUILD) `
                                -Region $env:GDK_COS_REGION `
                                -Bucket $env:GDK_COS_BUCKET `
                                -ResultPath $env:GDK_RELEASE_RESULT
                            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                        '''
                    }
                }
            }
        }

        stage('Promote Resource Pointer') {
            when {
                expression { return params.PROMOTE_RESOURCES }
            }
            steps {
                script {
                    if (!params.PUBLISH_RESOURCES) {
                        error('PROMOTE_RESOURCES requires PUBLISH_RESOURCES in the same build.')
                    }
                    if (!env.GDK_RESOURCE_SIGNING_CREDENTIAL_ID?.trim() ||
                        !env.GDK_RESOURCE_SIGNING_KEY_ID?.trim()) {
                        error('Resource signing job environment is incomplete.')
                    }
                    input(
                        message: "Promote ${params.CHANNEL}/${params.BUILD_TARGET}/${params.PLAYER_VERSION}?",
                        ok: 'Promote')
                    withCredentials([
                        usernamePassword(
                            credentialsId: env.GDK_COS_CREDENTIAL_ID,
                            usernameVariable: 'GDK_COS_SECRET_ID',
                            passwordVariable: 'GDK_COS_SECRET_KEY'),
                        file(
                            credentialsId: env.GDK_RESOURCE_SIGNING_CREDENTIAL_ID,
                            variable: 'GDK_RESOURCE_SIGNING_KEY_FILE')
                    ]) {
                        powershell '''
                            & pwsh -NoProfile -File "$env:WORKSPACE/Tools/CI/Jenkins/invoke-resource-promotion.ps1" `
                                -Channel $env:GDK_CHANNEL `
                                -Platform $env:GDK_BUILD_TARGET `
                                -Version $env:GDK_PLAYER_VERSION `
                                -Region $env:GDK_COS_REGION `
                                -Bucket $env:GDK_COS_BUCKET `
                                -KeyId $env:GDK_RESOURCE_SIGNING_KEY_ID `
                                -SigningKeyFile $env:GDK_RESOURCE_SIGNING_KEY_FILE `
                                -ResultPath $env:GDK_PROMOTION_RESULT
                            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
                        '''
                    }
                }
            }
        }
    }

    post {
        always {
            archiveArtifacts(
                artifacts: 'Build/Channel/channel-build-report.json,Build/Channel/staged-release.json,Build/Channel/promotion-result.json,Build/Channel/unity-editor.log,Build/Channel/quality-editmode.xml,Build/Channel/quality-editmode.log,Build/Channel/player/**/*',
                allowEmptyArchive: true,
                fingerprint: true)
        }
    }
}
