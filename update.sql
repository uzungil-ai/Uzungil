IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'00000000000000_CreateIdentitySchema', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [TableName] nvarchar(max) NOT NULL,
    [KeyValues] nvarchar(max) NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [Action] nvarchar(max) NOT NULL,
    [PerformedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [Itineraries] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_Itineraries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Itineraries_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Subscribers] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(max) NOT NULL,
    [ConsentGiven] bit NOT NULL,
    [IsConfirmed] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ConfirmationToken] nvarchar(max) NULL,
    CONSTRAINT [PK_Subscribers] PRIMARY KEY ([Id])
);

CREATE TABLE [Destinations] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Country] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [InstagramUrl] nvarchar(max) NULL,
    [FacebookUrl] nvarchar(max) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [EntranceFee] decimal(18,2) NULL,
    [ItineraryId] int NULL,
    CONSTRAINT [PK_Destinations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Destinations_Itineraries_ItineraryId] FOREIGN KEY ([ItineraryId]) REFERENCES [Itineraries] ([Id])
);

CREATE TABLE [Attractions] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Category] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    [AverageRating] float NOT NULL,
    [DestinationId] int NOT NULL,
    CONSTRAINT [PK_Attractions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Attractions_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DestinationImages] (
    [Id] int NOT NULL IDENTITY,
    [Url] nvarchar(max) NOT NULL,
    [DestinationId] int NOT NULL,
    CONSTRAINT [PK_DestinationImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DestinationImages_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Flavors] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [DestinationId] int NOT NULL,
    CONSTRAINT [PK_Flavors] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Flavors_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Hotels] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NULL,
    [Phone] nvarchar(max) NULL,
    [Website] nvarchar(max) NULL,
    [PriceFrom] decimal(18,2) NULL,
    [PriceTo] decimal(18,2) NULL,
    [DestinationId] int NOT NULL,
    CONSTRAINT [PK_Hotels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Hotels_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Meals] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Address] nvarchar(max) NULL,
    [Phone] nvarchar(max) NULL,
    [Price] decimal(18,2) NULL,
    [DestinationId] int NOT NULL,
    CONSTRAINT [PK_Meals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Meals_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Parkings] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Capacity] int NULL,
    [IsPaid] bit NOT NULL,
    [Fee] decimal(18,2) NULL,
    [HourlyRate] decimal(18,2) NULL,
    [DestinationId] int NOT NULL,
    CONSTRAINT [PK_Parkings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Parkings_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Recommendations] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Url] nvarchar(max) NULL,
    [UserId] nvarchar(max) NULL,
    [DestinationId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsOfficial] bit NOT NULL,
    CONSTRAINT [PK_Recommendations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Recommendations_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Reviews] (
    [Id] int NOT NULL IDENTITY,
    [Comment] nvarchar(max) NOT NULL,
    [Rating] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [AttractionId] int NOT NULL,
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Reviews_Attractions_AttractionId] FOREIGN KEY ([AttractionId]) REFERENCES [Attractions] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Attractions_DestinationId] ON [Attractions] ([DestinationId]);

CREATE INDEX [IX_DestinationImages_DestinationId] ON [DestinationImages] ([DestinationId]);

CREATE INDEX [IX_Destinations_ItineraryId] ON [Destinations] ([ItineraryId]);

CREATE INDEX [IX_Flavors_DestinationId] ON [Flavors] ([DestinationId]);

CREATE INDEX [IX_Hotels_DestinationId] ON [Hotels] ([DestinationId]);

CREATE INDEX [IX_Itineraries_UserId] ON [Itineraries] ([UserId]);

CREATE INDEX [IX_Meals_DestinationId] ON [Meals] ([DestinationId]);

CREATE INDEX [IX_Parkings_DestinationId] ON [Parkings] ([DestinationId]);

CREATE INDEX [IX_Recommendations_DestinationId] ON [Recommendations] ([DestinationId]);

CREATE INDEX [IX_Reviews_AttractionId] ON [Reviews] ([AttractionId]);

CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017023252_ApplyAllRecommendations', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscribers]') AND [c].[name] = N'Email');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Subscribers] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Subscribers] ALTER COLUMN [Email] nvarchar(450) NOT NULL;

ALTER TABLE [Subscribers] ADD [ConfirmationExpiresAt] datetime2 NULL;

ALTER TABLE [Subscribers] ADD [UnsubscribeToken] nvarchar(max) NULL;

CREATE TABLE [FailedEmails] (
    [Id] int NOT NULL IDENTITY,
    [To] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NULL,
    [Attempts] int NOT NULL,
    [LastError] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_FailedEmails] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Subscribers_Email] ON [Subscribers] ([Email]);

CREATE INDEX [IX_FailedEmails_CreatedAt] ON [FailedEmails] ([CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017023922_AddUnsubscribeAndDlq', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscribers]') AND [c].[name] = N'UnsubscribeToken');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Subscribers] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Subscribers] ALTER COLUMN [UnsubscribeToken] nvarchar(100) NULL;

CREATE TABLE [Feedbacks] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(max) NULL,
    [Message] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Feedbacks] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Feedbacks_CreatedAt] ON [Feedbacks] ([CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017024534_AddFeedbackAndDlqPages', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [TwoFactorCodes] (
    [Id] nvarchar(450) NOT NULL,
    [UserId] nvarchar(max) NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [Used] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_TwoFactorCodes] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017024747_AddTwoFactorLogin', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
EXEC sp_rename N'[TwoFactorCodes].[Code]', N'Salt', 'COLUMN';

ALTER TABLE [TwoFactorCodes] ADD [Attempts] int NOT NULL DEFAULT 0;

ALTER TABLE [TwoFactorCodes] ADD [HashedCode] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017024930_TwoFactorImprovements', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [DestinationImages] ADD [Order] int NOT NULL DEFAULT 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017030950_AddDestinationImageOrder', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [DownloadRecords] (
    [Id] int NOT NULL IDENTITY,
    [DestinationId] int NOT NULL,
    [ImageId] int NOT NULL,
    [UserId] nvarchar(max) NULL,
    [SessionId] nvarchar(max) NULL,
    [IsPaid] bit NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [DownloadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DownloadRecords] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017034038_AddAltTextAndDownloadRecord', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [DestinationImages] ADD [AltText] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017034154_PersistDestinationImageAltText', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017121400_AddMeals', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261017121602_AddDownloadAmountPrecision', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [DemoChatServices] (
    [Id] int NOT NULL IDENTITY,
    [Input] nvarchar(max) NOT NULL,
    [SessionId] nvarchar(max) NULL,
    [Reply] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DemoChatServices] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261020022911_AddDemoChatServices', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Reviews] ADD [IsApproved] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022190317_AddReviewApproval', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Reviews] ADD [IsRejected] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022191737_AddReviewRejectedFlag', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AiBudgetUsageStats] (
    [Id] int NOT NULL IDENTITY,
    [MonthKey] int NOT NULL,
    [EstimatedRequestCount] int NOT NULL,
    [EstimatedCostUsd] decimal(18,4) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AiBudgetUsageStats] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_AiBudgetUsageStats_MonthKey] ON [AiBudgetUsageStats] ([MonthKey]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261025085356_AddAiBudgetUsageStats', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SentEmails] (
    [Id] int NOT NULL IDENTITY,
    [To] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NULL,
    [SentAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SentEmails] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_SentEmails_SentAt] ON [SentEmails] ([SentAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030035037_AddSentEmails', N'10.0.14');

COMMIT;
GO

