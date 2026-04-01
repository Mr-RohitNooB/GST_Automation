USE [StudyCafeToolsDB]
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- TABLES  (all wrapped with IF NOT EXISTS so re-running is safe)
-- ══════════════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ActiveSessions')
BEGIN
    CREATE TABLE [dbo].[ActiveSessions](
        [SessionId]     [uniqueidentifier] NOT NULL,
        [UserId]        [int] NULL,
        [LoginTime]     [datetime] NULL,
        [LastHeartbeat] [datetime] NULL,
    PRIMARY KEY CLUSTERED ([SessionId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    );
    PRINT 'Created table: ActiveSessions';
END
ELSE PRINT 'Table ActiveSessions already exists — skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Modules')
BEGIN
    CREATE TABLE [dbo].[Modules](
        [ModuleId]   [int] IDENTITY(1,1) NOT NULL,
        [ModuleName] [nvarchar](100) NOT NULL,
        [Category]   [nvarchar](50) NULL,
        [IsActive]   [bit] NULL,
    PRIMARY KEY CLUSTERED ([ModuleId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    );
    PRINT 'Created table: Modules';
END
ELSE PRINT 'Table Modules already exists — skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AutomationScripts')
BEGIN
    CREATE TABLE [dbo].[AutomationScripts](
        [ScriptId]    [int] IDENTITY(1,1) NOT NULL,
        [ModuleId]    [int] NULL,
        [Version]     [nvarchar](50) NOT NULL,
        [JsonContent] [nvarchar](max) NOT NULL,
        [ReleaseDate] [datetime] NULL,
    PRIMARY KEY CLUSTERED ([ScriptId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    ) TEXTIMAGE_ON [PRIMARY];
    PRINT 'Created table: AutomationScripts';
END
ELSE PRINT 'Table AutomationScripts already exists — skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Packages')
BEGIN
    CREATE TABLE [dbo].[Packages](
        [PackageId]            [int] IDENTITY(1,1) NOT NULL,
        [PackageName]          [nvarchar](100) NOT NULL,
        [MaxSimultaneousUsers] [int] NULL,
    PRIMARY KEY CLUSTERED ([PackageId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    );
    PRINT 'Created table: Packages';
END
ELSE PRINT 'Table Packages already exists — skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PackageModules')
BEGIN
    CREATE TABLE [dbo].[PackageModules](
        [PackageId] [int] NOT NULL,
        [ModuleId]  [int] NOT NULL,
    PRIMARY KEY CLUSTERED ([PackageId] ASC, [ModuleId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    );
    PRINT 'Created table: PackageModules';
END
ELSE PRINT 'Table PackageModules already exists — skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users](
        [UserId]       [int] IDENTITY(1,1) NOT NULL,
        [Email]        [nvarchar](255) NOT NULL,
        [PasswordHash] [nvarchar](255) NOT NULL,
        [FullName]     [nvarchar](100) NULL,
        [IsActive]     [bit] NULL,
    PRIMARY KEY CLUSTERED ([UserId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY],
    UNIQUE NONCLUSTERED ([Email] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    );
    PRINT 'Created table: Users';
END
ELSE PRINT 'Table Users already exists — skipped.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserSubscriptions')
BEGIN
    CREATE TABLE [dbo].[UserSubscriptions](
        [SubscriptionId] [int] IDENTITY(1,1) NOT NULL,
        [UserId]         [int] NULL,
        [PackageId]      [int] NULL,
        [StartDate]      [datetime] NULL,
        [EndDate]        [datetime] NOT NULL,
        [IsActive]       [bit] NULL,
    PRIMARY KEY CLUSTERED ([SubscriptionId] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
          ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
    ON [PRIMARY]
    );
    PRINT 'Created table: UserSubscriptions';
END
ELSE PRINT 'Table UserSubscriptions already exists — skipped.';
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- DEFAULT CONSTRAINTS
-- Checks whether the COLUMN already has any default bound (regardless of
-- constraint name), so re-running is safe even when SQL Server auto-named them.
-- ══════════════════════════════════════════════════════════════════════════════

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'ActiveSessions' AND c.name = 'LoginTime')
    ALTER TABLE [dbo].[ActiveSessions] ADD CONSTRAINT [DF_ActiveSessions_LoginTime]     DEFAULT (GETUTCDATE()) FOR [LoginTime];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'ActiveSessions' AND c.name = 'LastHeartbeat')
    ALTER TABLE [dbo].[ActiveSessions] ADD CONSTRAINT [DF_ActiveSessions_LastHeartbeat] DEFAULT (GETUTCDATE()) FOR [LastHeartbeat];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'AutomationScripts' AND c.name = 'ReleaseDate')
    ALTER TABLE [dbo].[AutomationScripts] ADD CONSTRAINT [DF_AutomationScripts_ReleaseDate] DEFAULT (GETUTCDATE()) FOR [ReleaseDate];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Modules' AND c.name = 'IsActive')
    ALTER TABLE [dbo].[Modules] ADD CONSTRAINT [DF_Modules_IsActive]                   DEFAULT ((1)) FOR [IsActive];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Packages' AND c.name = 'MaxSimultaneousUsers')
    ALTER TABLE [dbo].[Packages] ADD CONSTRAINT [DF_Packages_MaxSimultaneousUsers]     DEFAULT ((5)) FOR [MaxSimultaneousUsers];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'Users' AND c.name = 'IsActive')
    ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_IsActive]                       DEFAULT ((1)) FOR [IsActive];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'UserSubscriptions' AND c.name = 'StartDate')
    ALTER TABLE [dbo].[UserSubscriptions] ADD CONSTRAINT [DF_UserSubscriptions_StartDate] DEFAULT (GETUTCDATE()) FOR [StartDate];
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE OBJECT_NAME(dc.parent_object_id) = 'UserSubscriptions' AND c.name = 'IsActive')
    ALTER TABLE [dbo].[UserSubscriptions] ADD CONSTRAINT [DF_UserSubscriptions_IsActive]  DEFAULT ((1)) FOR [IsActive];
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- FOREIGN KEYS  (each guarded by name so re-running is safe)
-- ══════════════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ActiveSessions_Users')
    ALTER TABLE [dbo].[ActiveSessions]
        ADD CONSTRAINT [FK_ActiveSessions_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AutomationScripts_Modules')
    ALTER TABLE [dbo].[AutomationScripts]
        ADD CONSTRAINT [FK_AutomationScripts_Modules] FOREIGN KEY ([ModuleId]) REFERENCES [dbo].[Modules] ([ModuleId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PackageModules_Modules')
    ALTER TABLE [dbo].[PackageModules]
        ADD CONSTRAINT [FK_PackageModules_Modules] FOREIGN KEY ([ModuleId]) REFERENCES [dbo].[Modules] ([ModuleId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PackageModules_Packages')
    ALTER TABLE [dbo].[PackageModules]
        ADD CONSTRAINT [FK_PackageModules_Packages] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[Packages] ([PackageId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserSubscriptions_Packages')
    ALTER TABLE [dbo].[UserSubscriptions]
        ADD CONSTRAINT [FK_UserSubscriptions_Packages] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[Packages] ([PackageId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UserSubscriptions_Users')
    ALTER TABLE [dbo].[UserSubscriptions]
        ADD CONSTRAINT [FK_UserSubscriptions_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]);
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- STORED PROCEDURES  (CREATE OR ALTER — safe to re-run any time)
-- ══════════════════════════════════════════════════════════════════════════════

-- Context: Grabs the tools a user is allowed to use based on their active subscription.
-- The API uses this to build the JWT.
CREATE OR ALTER PROCEDURE [dbo].[sp_GetAllowedModulesForUser]
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActivePackageId INT;

    SELECT TOP 1 @ActivePackageId = PackageId
    FROM UserSubscriptions
    WHERE UserId  = @UserId
      AND IsActive = 1
      AND EndDate  > GETUTCDATE()
    ORDER BY EndDate DESC;

    IF @ActivePackageId IS NULL
    BEGIN
        SELECT ModuleId, ModuleName FROM Modules WHERE 1 = 0;
        RETURN;
    END

    SELECT M.ModuleId, M.ModuleName, M.Category
    FROM   PackageModules PM
    INNER JOIN Modules M ON PM.ModuleId = M.ModuleId
    WHERE  PM.PackageId = @ActivePackageId
      AND  M.IsActive   = 1;
END
GO

-- Context: Checks if user exists by email, cleans dead sessions, enforces 5-device limit.
-- Password verification is done in the application layer (BCrypt), not here.
CREATE OR ALTER PROCEDURE [dbo].[sp_UserLoginAndSessionCheck]
    @Email        NVARCHAR(255),
    @NewSessionId UNIQUEIDENTIFIER OUTPUT,
    @LoginStatus  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT;

    SELECT @UserId = UserId
    FROM   Users
    WHERE  Email    = @Email
      AND  IsActive = 1;

    IF @UserId IS NULL
    BEGIN
        SET @LoginStatus = 0; -- Email not found / account inactive
        RETURN;
    END

    -- Remove sessions with no heartbeat for > 5 minutes
    DELETE FROM ActiveSessions
    WHERE  UserId        = @UserId
      AND  LastHeartbeat < DATEADD(minute, -5, GETUTCDATE());

    -- Enforce per-user concurrent session limit
    DECLARE @ActiveCount INT;
    SELECT @ActiveCount = COUNT(*) FROM ActiveSessions WHERE UserId = @UserId;

    IF @ActiveCount >= 5
    BEGIN
        SET @LoginStatus = -1; -- Max sessions reached
        RETURN;
    END

    -- Register new session
    SET @NewSessionId = NEWID();
    INSERT INTO ActiveSessions (SessionId, UserId, LoginTime, LastHeartbeat)
    VALUES (@NewSessionId, @UserId, GETUTCDATE(), GETUTCDATE());

    SET @LoginStatus = 1; -- Success
END
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- SEED DATA
-- Re-running is safe: each INSERT is guarded with IF NOT EXISTS.
-- When the GST portal changes layout, update JsonContent here — no DLL rebuild.
-- ══════════════════════════════════════════════════════════════════════════════

-- Module: GSTR-2B Downloader (must exist before AutomationScripts FK insert)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Modules] WHERE [ModuleName] = 'GSTR-2B Downloader')
BEGIN
    SET IDENTITY_INSERT [dbo].[Modules] OFF; -- let IDENTITY assign the ID
    INSERT INTO [dbo].[Modules] ([ModuleName], [Category], [IsActive])
    VALUES (N'GSTR-2B Downloader', N'GST', 1);
    PRINT 'Seeded module: GSTR-2B Downloader';
END
ELSE PRINT 'Module GSTR-2B Downloader already exists — skipped.';
GO

-- Automation script for GSTR-2B Downloader (version 1.0)
-- Look up the ModuleId dynamically so IDENTITY value doesn't need to be guessed.
DECLARE @ModuleId INT;
SELECT @ModuleId = ModuleId FROM [dbo].[Modules] WHERE [ModuleName] = N'GSTR-2B Downloader';

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[AutomationScripts]
    WHERE  ModuleId = @ModuleId AND Version = N'1.0'
)
BEGIN
    INSERT INTO [dbo].[AutomationScripts] ([ModuleId], [Version], [JsonContent], [ReleaseDate])
    VALUES (
        @ModuleId,
        N'1.0',
        N'{"moduleName":"GSTR-2B Downloader","version":"1.0","login":{"url":"https://services.gst.gov.in/services/login","usernameSelector":"#username","passwordSelector":"#user_pass","captchaImageSelector":"#imgCaptcha","captchaInputSelector":"#captcha","submitSelector":"button[type=submit]","successText":"Return Dashboard","invalidCaptchaText":"Invalid Captcha","invalidCredText":"Invalid Username or Password","aadharSkipSelector":"a >> text=Remind me later","genericSkipSelector":"button >> text=Remind Me Later","returnDashboardSelector":"button >> text=Return Dashboard","dashboardUrl":"https://return.gst.gov.in/returns/auth/dashboard"},"period":{"yearSelector":"[name=fin]","quarterSelector":"[name=quarter]","monthSelector":"[name=mon]","searchSelector":"//button[contains(text(),''Search'')]"},"download":{"quarterlyTileXpath":"//p[contains(text(),''Quarterly View'')]/ancestor::div[contains(@class,''col-sm-4'')]//button[contains(text(),''Download'')]","standardTileXpath":"//div[contains(@class,''col-sm-4'')]//p[contains(text(),''GSTR2B'')]/ancestor::div[contains(@class,''col-sm-4'')]//button[contains(text(),''Download'')]","generateBtnXpath":"//button[contains(text(),''GENERATE EXCEL FILE TO DOWNLOAD'')]","downloadLinkSelector":"a >> text=Click here to download","notGeneratedText":"compute your GSTR 2B","noRecordText":"no record"},"session":{"accessDeniedText":"Access Denied","expiredText":"session is expired","expiredUrlFragment":"accessdenied","loginPageIndicator":"#username"}}',
        GETUTCDATE()
    );
    PRINT 'Seeded AutomationScript: GSTR-2B Downloader v1.0';
END
ELSE PRINT 'AutomationScript GSTR-2B Downloader v1.0 already exists — skipped.';
GO
