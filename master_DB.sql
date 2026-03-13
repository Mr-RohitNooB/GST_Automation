-- =========================================
-- 1. TABLE CREATION
-- =========================================

CREATE TABLE Users (
    UserId INT PRIMARY KEY IDENTITY(1,1),
    Email NVARCHAR(255) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100),
    IsActive BIT DEFAULT 1
);

CREATE TABLE Packages (
    PackageId INT PRIMARY KEY IDENTITY(1,1),
    PackageName NVARCHAR(100) NOT NULL, 
    MaxSimultaneousUsers INT DEFAULT 5
);

CREATE TABLE Modules (
    ModuleId INT PRIMARY KEY IDENTITY(1,1),
    ModuleName NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50), 
    IsActive BIT DEFAULT 1
);

CREATE TABLE PackageModules (
    PackageId INT FOREIGN KEY REFERENCES Packages(PackageId),
    ModuleId INT FOREIGN KEY REFERENCES Modules(ModuleId),
    PRIMARY KEY (PackageId, ModuleId)
);

CREATE TABLE UserSubscriptions (
    SubscriptionId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT FOREIGN KEY REFERENCES Users(UserId),
    PackageId INT FOREIGN KEY REFERENCES Packages(PackageId),
    StartDate DATETIME DEFAULT GETUTCDATE(),
    EndDate DATETIME NOT NULL,
    IsActive BIT DEFAULT 1
);

CREATE TABLE ActiveSessions (
    SessionId UNIQUEIDENTIFIER PRIMARY KEY,
    UserId INT FOREIGN KEY REFERENCES Users(UserId),
    LoginTime DATETIME DEFAULT GETUTCDATE(),
    LastHeartbeat DATETIME DEFAULT GETUTCDATE()
);

CREATE TABLE AutomationScripts (
    ScriptId INT PRIMARY KEY IDENTITY(1,1),
    ModuleId INT FOREIGN KEY REFERENCES Modules(ModuleId),
    Version NVARCHAR(50) NOT NULL,
    JsonContent NVARCHAR(MAX) NOT NULL,
    ReleaseDate DATETIME DEFAULT GETUTCDATE()
);
GO

-- =========================================
-- 2. SEED DATA (Testing Setup)
-- =========================================

-- Insert Base Packages
INSERT INTO Packages (PackageName, MaxSimultaneousUsers)
VALUES ('Tax Pro Suite', 5), ('Marketing Suite', 5);

-- Insert Automation Modules
INSERT INTO Modules (ModuleName, Category)
VALUES 
('GST Downloader', 'Taxation'), 
('ITR Filer', 'Taxation'), 
('LinkedIn Scraper', 'Marketing');

-- Map Modules to Packages
-- Tax Pro Suite gets GST (1) and ITR (2)
INSERT INTO PackageModules (PackageId, ModuleId) VALUES (1, 1), (1, 2);
-- Marketing Suite gets LinkedIn (3)
INSERT INTO PackageModules (PackageId, ModuleId) VALUES (2, 3);

-- Create a Test User
-- Note: In a real scenario, the password MUST be hashed before inserting.
INSERT INTO Users (Email, PasswordHash, FullName)
VALUES ('rohitchauhaninfo@gmail.com', 'hashed_password_here', 'Rohit Chauhan');

-- Assign User to the Tax Pro Suite (Valid for 1 year)
INSERT INTO UserSubscriptions (UserId, PackageId, StartDate, EndDate)
VALUES (1, 1, GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()));
GO

-- =========================================
-- 3. STORED PROCEDURES
-- =========================================

-- Context: This procedure handles the login attempt. It checks credentials, 
-- clears out dead sessions, and ensures the user hasn't exceeded their limit.
-- Table Name: Users, ActiveSessions
CREATE PROCEDURE sp_UserLoginAndSessionCheck
    @Email NVARCHAR(255),
    @PasswordHash NVARCHAR(255), 
    @NewSessionId UNIQUEIDENTIFIER OUTPUT,
    @LoginStatus INT OUTPUT 
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT;

    SELECT @UserId = UserId 
    FROM Users 
    WHERE Email = @Email AND PasswordHash = @PasswordHash;

    IF @UserId IS NULL
    BEGIN
        SET @LoginStatus = 0; -- Invalid login
        RETURN;
    END

    DELETE FROM ActiveSessions 
    WHERE UserId = @UserId AND LastHeartbeat < DATEADD(minute, -5, GETUTCDATE());

    DECLARE @ActiveCount INT;
    SELECT @ActiveCount = COUNT(*) 
    FROM ActiveSessions 
    WHERE UserId = @UserId;

    IF @ActiveCount >= 5
    BEGIN
        SET @LoginStatus = -1; -- Max sessions reached
        RETURN;
    END

    SET @NewSessionId = NEWID();
    INSERT INTO ActiveSessions (SessionId, UserId, LoginTime, LastHeartbeat)
    VALUES (@NewSessionId, @UserId, GETUTCDATE(), GETUTCDATE());

    SET @LoginStatus = 1; -- Success
END
GO

-- Context: This procedure grabs the specific tools a user is allowed to use
-- based on their current, active subscription. The API uses this to build the JWT.
-- Table Name: UserSubscriptions, PackageModules, Modules
CREATE PROCEDURE sp_GetAllowedModulesForUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActivePackageId INT;

    SELECT TOP 1 @ActivePackageId = PackageId
    FROM UserSubscriptions
    WHERE UserId = @UserId 
      AND IsActive = 1 
      AND EndDate > GETUTCDATE()
    ORDER BY EndDate DESC; 

    IF @ActivePackageId IS NULL
    BEGIN
        SELECT ModuleId, ModuleName FROM Modules WHERE 1 = 0; 
        RETURN;
    END

    SELECT M.ModuleId, M.ModuleName, M.Category
    FROM PackageModules PM
    INNER JOIN Modules M ON PM.ModuleId = M.ModuleId
    WHERE PM.PackageId = @ActivePackageId
      AND M.IsActive = 1;
END
GO