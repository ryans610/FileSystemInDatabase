USE [FileSystem];

TRUNCATE TABLE [Node];
TRUNCATE TABLE [FolderNode];
TRUNCATE TABLE [FileNode];

INSERT INTO [Node]
([Id],[Name],[ParentId],[Type],[IsRoot])
VALUES
-- Roots
(CAST('2aa61413-0c8c-447a-99e6-b6981df0b315' AS uniqueidentifier),'C:',CAST('2aa61413-0c8c-447a-99e6-b6981df0b315' AS uniqueidentifier),0,1),

-- Folders
(CAST('4bedd362-185f-45e0-9b10-33f4a99c6c19' AS uniqueidentifier),'Users',CAST('2aa61413-0c8c-447a-99e6-b6981df0b315' AS uniqueidentifier),0,0),
(CAST('6a389c8e-ba2f-423c-9189-9315454fd4b7' AS uniqueidentifier),'Public',CAST('4bedd362-185f-45e0-9b10-33f4a99c6c19' AS uniqueidentifier),0,0),
(CAST('bbf12448-65f5-4115-b99f-d17a4fef2943' AS uniqueidentifier),'Documents',CAST('6a389c8e-ba2f-423c-9189-9315454fd4b7' AS uniqueidentifier),0,0),
(CAST('0a7d802a-53f7-4286-915a-7731d2fec53c' AS uniqueidentifier),'Pictures',CAST('6a389c8e-ba2f-423c-9189-9315454fd4b7' AS uniqueidentifier),0,0),
(CAST('d49772c6-af9d-4bd8-beb4-17fbec56e4b3' AS uniqueidentifier),'Ryan',CAST('4bedd362-185f-45e0-9b10-33f4a99c6c19' AS uniqueidentifier),0,0),
(CAST('ecd1cf15-8211-4f5f-bc51-f039ac6a0fec' AS uniqueidentifier),'Documents',CAST('d49772c6-af9d-4bd8-beb4-17fbec56e4b3' AS uniqueidentifier),0,0),
(CAST('025efaae-1127-496c-8f05-e83d390bd6e2' AS uniqueidentifier),'Pictures',CAST('d49772c6-af9d-4bd8-beb4-17fbec56e4b3' AS uniqueidentifier),0,0),

-- Files
(CAST('0188e2d7-ea03-4fbe-90f7-b989dad6b173' AS uniqueidentifier),'some_note',CAST('bbf12448-65f5-4115-b99f-d17a4fef2943' AS uniqueidentifier),1,0),
(CAST('58e8a31d-58d0-4564-897f-beca0a242b3c' AS uniqueidentifier),'some_note2',CAST('bbf12448-65f5-4115-b99f-d17a4fef2943' AS uniqueidentifier),1,0),
(CAST('7144b84a-e5ef-4b3d-94b7-764414458363' AS uniqueidentifier),'some_note.3',CAST('bbf12448-65f5-4115-b99f-d17a4fef2943' AS uniqueidentifier),1,0),
(CAST('e64ee1a2-8370-4852-8ccd-f5d07c6043ea' AS uniqueidentifier),'record20220315',CAST('ecd1cf15-8211-4f5f-bc51-f039ac6a0fec' AS uniqueidentifier),1,0),
(CAST('c75a4bd4-dc46-4a51-b5e8-267985d0a6f8' AS uniqueidentifier),'a photo',CAST('0a7d802a-53f7-4286-915a-7731d2fec53c' AS uniqueidentifier),1,0),
(CAST('cb720f82-55ef-480f-a779-408bec69e91c' AS uniqueidentifier),'',CAST('d49772c6-af9d-4bd8-beb4-17fbec56e4b3' AS uniqueidentifier),1,0),
(CAST('46aaffa2-9488-466d-969a-2a0721725dc5' AS uniqueidentifier),'',CAST('d49772c6-af9d-4bd8-beb4-17fbec56e4b3' AS uniqueidentifier),1,0)

INSERT INTO [FolderNode]
([Id])
VALUES
(CAST('2aa61413-0c8c-447a-99e6-b6981df0b315' AS uniqueidentifier)),
(CAST('4bedd362-185f-45e0-9b10-33f4a99c6c19' AS uniqueidentifier)),
(CAST('6a389c8e-ba2f-423c-9189-9315454fd4b7' AS uniqueidentifier)),
(CAST('bbf12448-65f5-4115-b99f-d17a4fef2943' AS uniqueidentifier)),
(CAST('0a7d802a-53f7-4286-915a-7731d2fec53c' AS uniqueidentifier)),
(CAST('d49772c6-af9d-4bd8-beb4-17fbec56e4b3' AS uniqueidentifier)),
(CAST('025efaae-1127-496c-8f05-e83d390bd6e2' AS uniqueidentifier))

DECLARE @DummyContent varbinary(MAX) = CONVERT(varbinary(MAX), 'foobar', 0)

INSERT INTO [FileNode]
([Id],[Extension],[Content])
VALUES
(CAST('0188e2d7-ea03-4fbe-90f7-b989dad6b173' AS uniqueidentifier),'.txt',@DummyContent),
(CAST('58e8a31d-58d0-4564-897f-beca0a242b3c' AS uniqueidentifier),'.txt',@DummyContent),
(CAST('7144b84a-e5ef-4b3d-94b7-764414458363' AS uniqueidentifier),'.txt',@DummyContent),
(CAST('e64ee1a2-8370-4852-8ccd-f5d07c6043ea' AS uniqueidentifier),'.docx',@DummyContent),
(CAST('c75a4bd4-dc46-4a51-b5e8-267985d0a6f8' AS uniqueidentifier),'.jpg',@DummyContent),
(CAST('cb720f82-55ef-480f-a779-408bec69e91c' AS uniqueidentifier),'.bash_profile',@DummyContent),
(CAST('46aaffa2-9488-466d-969a-2a0721725dc5' AS uniqueidentifier),'.bash_history',@DummyContent)
