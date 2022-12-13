IF (NOT EXISTS (SELECT *
                FROM sys.change_tracking_databases
                WHERE database_id = DB_ID('FileSystem')))
BEGIN
    ALTER DATABASE [FileSystem]
    SET CHANGE_TRACKING = ON
    (CHANGE_RETENTION = 1 HOURS, AUTO_CLEANUP = ON)
END

USE [FileSystem];

IF (NOT EXISTS (SELECT *
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                AND TABLE_NAME = 'Node'))
BEGIN
    CREATE TABLE [Node] (
        [Id] uniqueidentifier  PRIMARY KEY,
        [Name] nvarchar(255) NOT NULL,
        [ParentId] uniqueidentifier NOT NULL,
        [Type] tinyint NOT NULL,
        [IsRoot] bit NOT NULL DEFAULT 0,
    );
END

IF (NOT EXISTS (SELECT *
                FROM sys.indexes
                WHERE object_id = object_id('dbo.Node')
                AND [Name] = 'IX_Node_ParentId'))
BEGIN
    CREATE INDEX IX_Node_ParentId ON [dbo].[Node] ([ParentId]);
END

IF (NOT EXISTS (SELECT *
                FROM sys.change_tracking_tables
                WHERE object_id = OBJECT_ID('dbo.Node')))
BEGIN
    ALTER TABLE [Node]
    ENABLE CHANGE_TRACKING
    WITH (TRACK_COLUMNS_UPDATED = ON)
END

IF (NOT EXISTS (SELECT *
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                AND TABLE_NAME = 'FolderNode'))
BEGIN
    CREATE TABLE [FolderNode](
        [Id] uniqueidentifier  PRIMARY KEY,
    );
END

IF (NOT EXISTS (SELECT *
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                AND TABLE_NAME = 'FileNode'))
BEGIN
    CREATE TABLE [FileNode] (
        [Id] uniqueidentifier  PRIMARY KEY,
        [Extension] varchar(255) NOT NULL,
        [Content] varbinary(MAX) NOT NULL,
    );
END
