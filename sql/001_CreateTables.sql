-- ScheduleAdjust Database Schema
-- SQL Server Migration Script

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchedulePolls')
BEGIN
    CREATE TABLE [SchedulePolls] (
        [PollId] INT IDENTITY(1,1) NOT NULL,
        [PollGuid] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [DurationMinutes] INT NOT NULL,
        [CandidateStartDate] DATETIMEOFFSET NOT NULL,
        [CandidateEndDate] DATETIMEOFFSET NOT NULL,
        [Deadline] DATETIMEOFFSET NULL,
        [Note] NVARCHAR(2000) NULL,
        [Status] INT NOT NULL DEFAULT 0,
        [OrganizerId] NVARCHAR(100) NOT NULL,
        [OrganizerEmail] NVARCHAR(256) NOT NULL,
        [OrganizerName] NVARCHAR(200) NOT NULL,
        [ConfirmedSlotId] INT NULL,
        [GraphEventId] NVARCHAR(500) NULL,
        [TeamsJoinUrl] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [UpdatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_SchedulePolls] PRIMARY KEY ([PollId])
    );

    CREATE UNIQUE INDEX [IX_SchedulePolls_PollGuid] ON [SchedulePolls] ([PollGuid]);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PollAttendees')
BEGIN
    CREATE TABLE [PollAttendees] (
        [AttendeeId] INT IDENTITY(1,1) NOT NULL,
        [PollId] INT NOT NULL,
        [UserObjectId] NVARCHAR(100) NOT NULL DEFAULT '',
        [Email] NVARCHAR(256) NOT NULL,
        [DisplayName] NVARCHAR(200) NOT NULL,
        [IsRequired] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_PollAttendees] PRIMARY KEY ([AttendeeId]),
        CONSTRAINT [FK_PollAttendees_SchedulePolls] FOREIGN KEY ([PollId])
            REFERENCES [SchedulePolls] ([PollId]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_PollAttendees_PollId_Email] ON [PollAttendees] ([PollId], [Email]);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PollTimeSlots')
BEGIN
    CREATE TABLE [PollTimeSlots] (
        [SlotId] INT IDENTITY(1,1) NOT NULL,
        [PollId] INT NOT NULL,
        [StartDateTime] DATETIMEOFFSET NOT NULL,
        [EndDateTime] DATETIMEOFFSET NOT NULL,
        [IsManuallyAdded] BIT NOT NULL DEFAULT 0,
        [IsAvailable] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_PollTimeSlots] PRIMARY KEY ([SlotId]),
        CONSTRAINT [FK_PollTimeSlots_SchedulePolls] FOREIGN KEY ([PollId])
            REFERENCES [SchedulePolls] ([PollId]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_PollTimeSlots_PollId] ON [PollTimeSlots] ([PollId]);
END;

-- Add FK from SchedulePolls.ConfirmedSlotId to PollTimeSlots after PollTimeSlots is created
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_SchedulePolls_ConfirmedSlot')
BEGIN
    ALTER TABLE [SchedulePolls]
        ADD CONSTRAINT [FK_SchedulePolls_ConfirmedSlot] FOREIGN KEY ([ConfirmedSlotId])
            REFERENCES [PollTimeSlots] ([SlotId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PollResponses')
BEGIN
    CREATE TABLE [PollResponses] (
        [ResponseId] INT IDENTITY(1,1) NOT NULL,
        [PollId] INT NOT NULL,
        [SelectedSlotId] INT NOT NULL,
        [RespondentName] NVARCHAR(200) NOT NULL,
        [RespondentEmail] NVARCHAR(256) NOT NULL,
        [RespondentCompany] NVARCHAR(200) NULL,
        [RespondedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT [PK_PollResponses] PRIMARY KEY ([ResponseId]),
        CONSTRAINT [FK_PollResponses_SchedulePolls] FOREIGN KEY ([PollId])
            REFERENCES [SchedulePolls] ([PollId]) ON DELETE CASCADE,
        CONSTRAINT [FK_PollResponses_PollTimeSlots] FOREIGN KEY ([SelectedSlotId])
            REFERENCES [PollTimeSlots] ([SlotId]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_PollResponses_PollId] ON [PollResponses] ([PollId]);
END;
