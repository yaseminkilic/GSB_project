IF DB_ID(N'SportsEvents') IS NULL
    EXEC(N'CREATE DATABASE [SportsEvents]');
GO

USE [SportsEvents];
GO

CREATE TABLE dbo.[Event] (
    Id int IDENTITY PRIMARY KEY,
    Name nvarchar(150) NOT NULL,
    StartAt datetime2(0) NOT NULL,
    Capacity int NOT NULL CHECK (Capacity > 0),
    RegisteredCount int NOT NULL DEFAULT (0),
    CONSTRAINT CK_Event_RegisteredCount
        CHECK (RegisteredCount >= 0 AND RegisteredCount <= Capacity)
);

CREATE TABLE dbo.Participant (
    Id int IDENTITY PRIMARY KEY,
    FullName nvarchar(150) NOT NULL
);

CREATE TABLE dbo.Registration (
    Id int IDENTITY PRIMARY KEY,
    EventId int NOT NULL REFERENCES dbo.[Event](Id),
    ParticipantId int NOT NULL REFERENCES dbo.Participant(Id),
    RegisteredAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    IsCancelled bit NOT NULL DEFAULT (0),
    CONSTRAINT UQ_Registration_Event_Participant
        UNIQUE (EventId, ParticipantId)
);

CREATE INDEX IX_Event_StartAt_Id ON dbo.[Event](StartAt, Id);
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT dbo.[Event] (Name, StartAt, Capacity, RegisteredCount)
VALUES (N'Yüzme', '2026-11-16T09:00:00', 2, 1);

DECLARE @SwimmingId int = CONVERT(int, SCOPE_IDENTITY());

INSERT dbo.[Event] (Name, StartAt, Capacity)
VALUES (N'Basketbol', '2026-11-16T11:00:00', 10),
       (N'Voleybol',  '2026-11-17T14:00:00', 8),
       (N'Tenis',     '2026-11-18T10:00:00', 4);

INSERT dbo.Participant (FullName) VALUES (N'Elif Yılmaz');
DECLARE @ElifId int = CONVERT(int, SCOPE_IDENTITY());

INSERT dbo.Participant (FullName)
VALUES (N'Can Demir'), (N'Deniz Kaya');

INSERT dbo.Registration (EventId, ParticipantId)
VALUES (@SwimmingId, @ElifId);

COMMIT TRANSACTION;

SELECT Id, Name, StartAt, Capacity, RegisteredCount FROM dbo.[Event];
SELECT Id, FullName FROM dbo.Participant;