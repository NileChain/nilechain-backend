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
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] uniqueidentifier NOT NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
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

CREATE TABLE [Certification] (
    [CertificationId] uniqueidentifier NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    CONSTRAINT [PK_Certification] PRIMARY KEY ([CertificationId])
);

CREATE TABLE [CropType] (
    [CropTypeId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_CropType] PRIMARY KEY ([CropTypeId])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Factory] (
    [FactoryId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [Location] nvarchar(255) NULL,
    [Governorate] nvarchar(100) NULL,
    [IndustryType] nvarchar(100) NULL,
    [AverageRating] decimal(3,2) NOT NULL,
    [RatingCount] int NOT NULL,
    [IsVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Factory] PRIMARY KEY ([FactoryId]),
    CONSTRAINT [FK_Factory_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Farm] (
    [FarmId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [Location] nvarchar(255) NULL,
    [Governorate] nvarchar(100) NULL,
    [SizeInFeddans] decimal(10,2) NULL,
    [RiskScore] decimal(5,2) NULL,
    [AverageRating] decimal(3,2) NOT NULL,
    [RatingCount] int NOT NULL,
    [IsVerified] bit NOT NULL,
    [ProfileComplete] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Farm] PRIMARY KEY ([FarmId]),
    CONSTRAINT [FK_Farm_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Notification] (
    [NotificationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Title] nvarchar(255) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Type] nvarchar(20) NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Notification] PRIMARY KEY ([NotificationId]),
    CONSTRAINT [FK_Notification_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RagDocument] (
    [DocumentId] uniqueidentifier NOT NULL,
    [Title] nvarchar(255) NOT NULL,
    [Category] nvarchar(30) NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [UploadedBy] uniqueidentifier NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_RagDocument] PRIMARY KEY ([DocumentId]),
    CONSTRAINT [FK_RagDocument_AspNetUsers_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [MarketPrice] (
    [PriceId] uniqueidentifier NOT NULL,
    [CropTypeId] uniqueidentifier NOT NULL,
    [Governorate] nvarchar(100) NULL,
    [PricePerTon] decimal(10,2) NOT NULL,
    [Source] nvarchar(50) NULL,
    [RecordedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MarketPrice] PRIMARY KEY ([PriceId]),
    CONSTRAINT [FK_MarketPrice_CropType_CropTypeId] FOREIGN KEY ([CropTypeId]) REFERENCES [CropType] ([CropTypeId]) ON DELETE NO ACTION
);

CREATE TABLE [SupplyRequest] (
    [RequestId] uniqueidentifier NOT NULL,
    [FactoryId] uniqueidentifier NOT NULL,
    [CropTypeId] uniqueidentifier NOT NULL,
    [QuantityTons] decimal(10,2) NOT NULL,
    [QualitySpecs] nvarchar(max) NULL,
    [PricePerTon] decimal(10,2) NULL,
    [DeliveryDate] datetime2 NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SupplyRequest] PRIMARY KEY ([RequestId]),
    CONSTRAINT [FK_SupplyRequest_CropType_CropTypeId] FOREIGN KEY ([CropTypeId]) REFERENCES [CropType] ([CropTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplyRequest_Factory_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Factory] ([FactoryId]) ON DELETE CASCADE
);

CREATE TABLE [FarmCertification] (
    [FarmId] uniqueidentifier NOT NULL,
    [CertificationId] uniqueidentifier NOT NULL,
    [IssuedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NULL,
    CONSTRAINT [PK_FarmCertification] PRIMARY KEY ([FarmId], [CertificationId]),
    CONSTRAINT [CK_FarmCertification_Dates] CHECK ([ExpiresAt] IS NULL OR [ExpiresAt] > [IssuedAt]),
    CONSTRAINT [FK_FarmCertification_Certification_CertificationId] FOREIGN KEY ([CertificationId]) REFERENCES [Certification] ([CertificationId]) ON DELETE CASCADE,
    CONSTRAINT [FK_FarmCertification_Farm_FarmId] FOREIGN KEY ([FarmId]) REFERENCES [Farm] ([FarmId]) ON DELETE CASCADE
);

CREATE TABLE [FarmCropType] (
    [CropTypesCropTypeId] uniqueidentifier NOT NULL,
    [FarmsFarmId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_FarmCropType] PRIMARY KEY ([CropTypesCropTypeId], [FarmsFarmId]),
    CONSTRAINT [FK_FarmCropType_CropType_CropTypesCropTypeId] FOREIGN KEY ([CropTypesCropTypeId]) REFERENCES [CropType] ([CropTypeId]) ON DELETE CASCADE,
    CONSTRAINT [FK_FarmCropType_Farm_FarmsFarmId] FOREIGN KEY ([FarmsFarmId]) REFERENCES [Farm] ([FarmId]) ON DELETE CASCADE
);

CREATE TABLE [RiskAssessmentReport] (
    [ReportId] uniqueidentifier NOT NULL,
    [FarmId] uniqueidentifier NOT NULL,
    [GeneratedText] nvarchar(max) NULL,
    [FactorsBreakdown] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_RiskAssessmentReport] PRIMARY KEY ([ReportId]),
    CONSTRAINT [FK_RiskAssessmentReport_Farm_FarmId] FOREIGN KEY ([FarmId]) REFERENCES [Farm] ([FarmId]) ON DELETE CASCADE
);

CREATE TABLE [ComparisonReport] (
    [ReportId] uniqueidentifier NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [GeneratedText] nvarchar(max) NULL,
    [PdfUrl] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ComparisonReport] PRIMARY KEY ([ReportId]),
    CONSTRAINT [FK_ComparisonReport_SupplyRequest_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [SupplyRequest] ([RequestId]) ON DELETE CASCADE
);

CREATE TABLE [FarmMatch] (
    [MatchId] uniqueidentifier NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [FarmId] uniqueidentifier NOT NULL,
    [MatchScore] decimal(5,2) NULL,
    [RiskScore] decimal(5,2) NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_FarmMatch] PRIMARY KEY ([MatchId]),
    CONSTRAINT [FK_FarmMatch_Farm_FarmId] FOREIGN KEY ([FarmId]) REFERENCES [Farm] ([FarmId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_FarmMatch_SupplyRequest_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [SupplyRequest] ([RequestId]) ON DELETE CASCADE
);

CREATE TABLE [Contract] (
    [ContractId] uniqueidentifier NOT NULL,
    [MatchId] uniqueidentifier NOT NULL,
    [GeneratedText] nvarchar(max) NULL,
    [PdfUrl] nvarchar(max) NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [SignedAt] datetime2 NULL,
    CONSTRAINT [PK_Contract] PRIMARY KEY ([ContractId]),
    CONSTRAINT [FK_Contract_FarmMatch_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [FarmMatch] ([MatchId]) ON DELETE CASCADE
);

CREATE TABLE [Message] (
    [MessageId] uniqueidentifier NOT NULL,
    [MatchId] uniqueidentifier NOT NULL,
    [SenderId] uniqueidentifier NOT NULL,
    [ReceiverId] uniqueidentifier NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Message] PRIMARY KEY ([MessageId]),
    CONSTRAINT [FK_Message_AspNetUsers_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Message_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Message_FarmMatch_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [FarmMatch] ([MatchId]) ON DELETE CASCADE
);

CREATE TABLE [ContractRagDocument] (
    [ContractsContractId] uniqueidentifier NOT NULL,
    [RagDocumentsDocumentId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ContractRagDocument] PRIMARY KEY ([ContractsContractId], [RagDocumentsDocumentId]),
    CONSTRAINT [FK_ContractRagDocument_Contract_ContractsContractId] FOREIGN KEY ([ContractsContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ContractRagDocument_RagDocument_RagDocumentsDocumentId] FOREIGN KEY ([RagDocumentsDocumentId]) REFERENCES [RagDocument] ([DocumentId]) ON DELETE CASCADE
);

CREATE TABLE [Review] (
    [ReviewId] uniqueidentifier NOT NULL,
    [ContractId] uniqueidentifier NOT NULL,
    [ReviewerId] uniqueidentifier NOT NULL,
    [TargetId] uniqueidentifier NOT NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Review] PRIMARY KEY ([ReviewId]),
    CONSTRAINT [CK_Review_Rating] CHECK ([Rating] BETWEEN 1 AND 5),
    CONSTRAINT [FK_Review_AspNetUsers_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Review_AspNetUsers_TargetId] FOREIGN KEY ([TargetId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Review_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE CASCADE
);

CREATE TABLE [Transactions] (
    [TransactionId] uniqueidentifier NOT NULL,
    [ContractId] uniqueidentifier NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [PaymentMethod] nvarchar(50) NULL,
    [PaidAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Transactions] PRIMARY KEY ([TransactionId]),
    CONSTRAINT [FK_Transactions_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contract] ([ContractId]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Certification_Name] ON [Certification] ([Name]);

CREATE INDEX [IX_ComparisonReport_RequestId] ON [ComparisonReport] ([RequestId]);

CREATE UNIQUE INDEX [IX_Contract_MatchId] ON [Contract] ([MatchId]);

CREATE INDEX [IX_ContractRagDocument_RagDocumentsDocumentId] ON [ContractRagDocument] ([RagDocumentsDocumentId]);

CREATE UNIQUE INDEX [IX_CropType_Name] ON [CropType] ([Name]);

CREATE UNIQUE INDEX [IX_Factory_UserId] ON [Factory] ([UserId]);

CREATE UNIQUE INDEX [IX_Farm_UserId] ON [Farm] ([UserId]);

CREATE INDEX [IX_FarmCertification_CertificationId] ON [FarmCertification] ([CertificationId]);

CREATE INDEX [IX_FarmCropType_FarmsFarmId] ON [FarmCropType] ([FarmsFarmId]);

CREATE INDEX [IX_FarmMatch_FarmId] ON [FarmMatch] ([FarmId]);

CREATE INDEX [IX_FarmMatch_RequestId] ON [FarmMatch] ([RequestId]);

CREATE INDEX [IX_MarketPrice_CropTypeId] ON [MarketPrice] ([CropTypeId]);

CREATE INDEX [IX_Message_MatchId] ON [Message] ([MatchId]);

CREATE INDEX [IX_Message_ReceiverId] ON [Message] ([ReceiverId]);

CREATE INDEX [IX_Message_SenderId] ON [Message] ([SenderId]);

CREATE INDEX [IX_Notification_UserId] ON [Notification] ([UserId]);

CREATE INDEX [IX_RagDocument_UploadedBy] ON [RagDocument] ([UploadedBy]);

CREATE INDEX [IX_Review_ContractId] ON [Review] ([ContractId]);

CREATE INDEX [IX_Review_ReviewerId] ON [Review] ([ReviewerId]);

CREATE INDEX [IX_Review_TargetId] ON [Review] ([TargetId]);

CREATE INDEX [IX_RiskAssessmentReport_FarmId] ON [RiskAssessmentReport] ([FarmId]);

CREATE INDEX [IX_SupplyRequest_CropTypeId] ON [SupplyRequest] ([CropTypeId]);

CREATE INDEX [IX_SupplyRequest_FactoryId] ON [SupplyRequest] ([FactoryId]);

CREATE INDEX [IX_Transactions_ContractId] ON [Transactions] ([ContractId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716072936_InitialCreate', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(512) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [RevokedAt] datetime2 NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716223603_AddCompanyVerification', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Farm] ADD [SoilType] nvarchar(50) NULL;

CREATE TABLE [FarmDocument] (
    [FarmDocumentId] uniqueidentifier NOT NULL,
    [FarmId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(500) NOT NULL,
    [FileUrl] nvarchar(2048) NOT NULL,
    [FileSize] bigint NOT NULL,
    [FileType] nvarchar(100) NOT NULL,
    [PublicId] nvarchar(500) NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_FarmDocument] PRIMARY KEY ([FarmDocumentId]),
    CONSTRAINT [FK_FarmDocument_Farm_FarmId] FOREIGN KEY ([FarmId]) REFERENCES [Farm] ([FarmId]) ON DELETE CASCADE
);

CREATE INDEX [IX_FarmDocument_FarmId] ON [FarmDocument] ([FarmId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260720132434_AddFarmSoilTypeAndDocuments', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [AspNetUsers] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);

UPDATE [AspNetUsers] SET [IsActive] = 1

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260729101310_AddUserIsActive', N'10.0.10');

COMMIT;
GO

