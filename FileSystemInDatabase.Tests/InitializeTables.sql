USE [FileSystem];

IF (NOT EXISTS (SELECT *
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                AND  TABLE_NAME = 'Node'))
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
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                AND  TABLE_NAME = 'FolderNode'))
BEGIN
    CREATE TABLE [FolderNode](
        [Id] uniqueidentifier  PRIMARY KEY,
    );
END

IF (NOT EXISTS (SELECT *
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo'
                AND  TABLE_NAME = 'FileNode'))
BEGIN
    CREATE TABLE [FileNode] (
        [Id] uniqueidentifier  PRIMARY KEY,
        [Extension] varchar(255) NOT NULL,
        [Content] varbinary(MAX) NOT NULL,
    );
END
