@{
    RootModule = 'AwsScheduleExpressionValidator.PsModule.dll'
    ModuleVersion = '1.2.0'
    GUID = '9f1a6ef5-2f7c-4fe5-96c2-5c1b41c83a6d'
    Author = 'Rob Green Engineering Ltd'
    CompanyName = 'Rob Green Engineering Ltd'
    Copyright = '(c) Rob Green Engineering Ltd. All rights reserved.'
    Description = 'PowerShell module for validating AWS EventBridge Scheduler expressions.'
    PowerShellVersion = '7.2'
    HelpInfoURI = 'https://github.com/trossr32/aws-schedule-expression-validator'
    CmdletsToExport = @(
        'Test-AwsScheduleExpressionFormat',
        'Test-AwsScheduleExpression',
        'Get-AwsScheduleExpressionOccurrence'
    )

    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags       = @('AWS', 'EventBridge', 'cron', 'Scheduler', 'Validation', 'PowerShell')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/trossr32/aws-schedule-expression-validator/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/trossr32/ps-transmission'

            # A URL to an icon representing this module.
            IconUri    = 'https://trossr32.github.io/aws-schedule-expression-validator/favicon-88x88.png'
        }
    }
}
