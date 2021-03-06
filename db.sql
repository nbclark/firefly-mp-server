USE [master]
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'GameServer')
BEGIN
CREATE DATABASE [GameServer] ON  PRIMARY 
( NAME = N'GameServer', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL.1\MSSQL\DATA\GameServer.mdf' , SIZE = 2048KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
 LOG ON 
( NAME = N'GameServer_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL.1\MSSQL\DATA\GameServer_log.ldf' , SIZE = 1024KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
END

GO
EXEC dbo.sp_dbcmptlevel @dbname=N'GameServer', @new_cmptlevel=90
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [GameServer].[dbo].[sp_fulltext_database] @action = 'disable'
end
GO
ALTER DATABASE [GameServer] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [GameServer] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [GameServer] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [GameServer] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [GameServer] SET ARITHABORT OFF 
GO
ALTER DATABASE [GameServer] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [GameServer] SET AUTO_CREATE_STATISTICS ON 
GO
ALTER DATABASE [GameServer] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [GameServer] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [GameServer] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [GameServer] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [GameServer] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [GameServer] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [GameServer] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [GameServer] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [GameServer] SET  ENABLE_BROKER 
GO
ALTER DATABASE [GameServer] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [GameServer] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [GameServer] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [GameServer] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [GameServer] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [GameServer] SET  READ_WRITE 
GO
ALTER DATABASE [GameServer] SET RECOVERY FULL 
GO
ALTER DATABASE [GameServer] SET  MULTI_USER 
GO
ALTER DATABASE [GameServer] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [GameServer] SET DB_CHAINING OFF 
USE [GameServer]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameSelectCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameSelectCommand]
AS
	SET NOCOUNT ON;
SELECT * FROM games' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameInsertCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameInsertCommand]
(
	@pkGame uniqueidentifier,
	@User nvarchar(50),
	@MapData varbinary(MAX),
	@Latitude float,
	@Longitude float,
	@Width int,
	@Height int,
	@Date datetime,
	@Ready bit,
	@Completed bit
)
AS
	SET NOCOUNT OFF;
INSERT INTO [games] ([pkGame], [User], [MapData], [Latitude], [Longitude], [Width], [Height], [Date], [Ready], [Completed]) VALUES (@pkGame, @User, @MapData, @Latitude, @Longitude, @Width, @Height, @Date, @Ready, @Completed);
	
SELECT pkGame, [User], MapData, Latitude, Longitude, Width, Height, Date, Ready, Completed FROM games WHERE (pkGame = @pkGame)' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameUpdateCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameUpdateCommand]
(
	@pkGame uniqueidentifier,
	@User nvarchar(50),
	@MapData varbinary(MAX),
	@Latitude float,
	@Longitude float,
	@Width int,
	@Height int,
	@Date datetime,
	@Ready bit,
	@Completed bit,
	@Original_pkGame uniqueidentifier,
	@Original_User nvarchar(50),
	@Original_Latitude float,
	@Original_Longitude float,
	@Original_Width int,
	@Original_Height int,
	@Original_Date datetime,
	@Original_Ready bit,
	@Original_Completed bit
)
AS
	SET NOCOUNT OFF;
UPDATE [games] SET [pkGame] = @pkGame, [User] = @User, [MapData] = @MapData, [Latitude] = @Latitude, [Longitude] = @Longitude, [Width] = @Width, [Height] = @Height, [Date] = @Date, [Ready] = @Ready, [Completed] = @Completed WHERE (([pkGame] = @Original_pkGame) AND ([User] = @Original_User) AND ([Latitude] = @Original_Latitude) AND ([Longitude] = @Original_Longitude) AND ([Width] = @Original_Width) AND ([Height] = @Original_Height) AND ([Date] = @Original_Date) AND ([Ready] = @Original_Ready) AND ([Completed] = @Original_Completed));
	
SELECT pkGame, [User], MapData, Latitude, Longitude, Width, Height, Date, Ready, Completed FROM games WHERE (pkGame = @pkGame)' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameDeleteCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameDeleteCommand]
(
	@Original_pkGame uniqueidentifier,
	@Original_User nvarchar(50),
	@Original_Latitude float,
	@Original_Longitude float,
	@Original_Width int,
	@Original_Height int,
	@Original_Date datetime,
	@Original_Ready bit,
	@Original_Completed bit
)
AS
	SET NOCOUNT OFF;
DELETE FROM [games] WHERE (([pkGame] = @Original_pkGame) AND ([User] = @Original_User) AND ([Latitude] = @Original_Latitude) AND ([Longitude] = @Original_Longitude) AND ([Width] = @Original_Width) AND ([Height] = @Original_Height) AND ([Date] = @Original_Date) AND ([Ready] = @Original_Ready) AND ([Completed] = @Original_Completed))' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[games]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[games](
	[pkGame] [uniqueidentifier] NOT NULL,
	[User] [nvarchar](50) NOT NULL,
	[MapData] [nvarchar](max) NOT NULL,
	[Latitude] [float] NOT NULL,
	[Longitude] [float] NOT NULL,
	[Width] [int] NOT NULL,
	[Height] [int] NOT NULL,
	[Date] [datetime] NOT NULL CONSTRAINT [DF_games_Date]  DEFAULT (getdate()),
	[Ready] [bit] NOT NULL CONSTRAINT [DF_games_Ready]  DEFAULT ((0)),
	[Completed] [bit] NOT NULL CONSTRAINT [DF_games_Completed]  DEFAULT ((0)),
 CONSTRAINT [PK_games] PRIMARY KEY CLUSTERED 
(
	[pkGame] ASC
)WITH (IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[gameStates]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[gameStates](
	[pkGameState] [uniqueidentifier] NOT NULL,
	[idGame] [uniqueidentifier] NOT NULL,
	[Device] [nvarchar](50) NOT NULL,
	[Master] [bit] NOT NULL,
	[Data] [varbinary](max) NOT NULL,
	[Received] [bit] NOT NULL,
	[Date] [datetime] NOT NULL CONSTRAINT [DF_gameStates_Date]  DEFAULT (getdate()),
	[Active] [bit] NOT NULL,
 CONSTRAINT [PK_gameStates] PRIMARY KEY CLUSTERED 
(
	[pkGameState] ASC
)WITH (IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[gameDevices]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[gameDevices](
	[pkGameDevice] [uniqueidentifier] NOT NULL,
	[idGame] [uniqueidentifier] NOT NULL,
	[idLastState] [uniqueidentifier] NOT NULL,
	[Device] [nvarchar](50) NOT NULL,
	[User] [nvarchar](50) NOT NULL,
	[Latitude] [float] NOT NULL,
	[Longitude] [float] NOT NULL,
	[Accepted] [bit] NOT NULL,
	[Ready] [bit] NOT NULL,
	[Date] [datetime] NOT NULL CONSTRAINT [DF_gameDevices_Date]  DEFAULT (getdate()),
 CONSTRAINT [PK_gameDevices] PRIMARY KEY CLUSTERED 
(
	[pkGameDevice] ASC
)WITH (IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameStateSelectCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameStateSelectCommand]
AS
	SET NOCOUNT ON;
select * from gamestates' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameStateInsertCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameStateInsertCommand]
(
	@pkGameState uniqueidentifier,
	@idGame uniqueidentifier,
	@Device nvarchar(50),
	@Master bit,
	@Data varbinary(MAX),
	@Received bit,
	@Date datetime,
	@Active bit
)
AS
	SET NOCOUNT OFF;
INSERT INTO [gamestates] ([pkGameState], [idGame], [Device], [Master], [Data], [Received], [Date], [Active]) VALUES (@pkGameState, @idGame, @Device, @Master, @Data, @Received, @Date, @Active);
	
SELECT pkGameState, idGame, Device, Master, Data, Received, Date, Active FROM gameStates WHERE (pkGameState = @pkGameState)' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameStateUpdateCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameStateUpdateCommand]
(
	@pkGameState uniqueidentifier,
	@idGame uniqueidentifier,
	@Device nvarchar(50),
	@Master bit,
	@Data varbinary(MAX),
	@Received bit,
	@Date datetime,
	@Active bit,
	@Original_pkGameState uniqueidentifier,
	@Original_idGame uniqueidentifier,
	@Original_Device nvarchar(50),
	@Original_Master bit,
	@Original_Received bit,
	@Original_Date datetime,
	@Original_Active bit
)
AS
	SET NOCOUNT OFF;
UPDATE [gamestates] SET [pkGameState] = @pkGameState, [idGame] = @idGame, [Device] = @Device, [Master] = @Master, [Data] = @Data, [Received] = @Received, [Date] = @Date, [Active] = @Active WHERE (([pkGameState] = @Original_pkGameState) AND ([idGame] = @Original_idGame) AND ([Device] = @Original_Device) AND ([Master] = @Original_Master) AND ([Received] = @Original_Received) AND ([Date] = @Original_Date) AND ([Active] = @Original_Active));
	
SELECT pkGameState, idGame, Device, Master, Data, Received, Date, Active FROM gameStates WHERE (pkGameState = @pkGameState)' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameStateDeleteCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameStateDeleteCommand]
(
	@Original_pkGameState uniqueidentifier,
	@Original_idGame uniqueidentifier,
	@Original_Device nvarchar(50),
	@Original_Master bit,
	@Original_Received bit,
	@Original_Date datetime,
	@Original_Active bit
)
AS
	SET NOCOUNT OFF;
DELETE FROM [gamestates] WHERE (([pkGameState] = @Original_pkGameState) AND ([idGame] = @Original_idGame) AND ([Device] = @Original_Device) AND ([Master] = @Original_Master) AND ([Received] = @Original_Received) AND ([Date] = @Original_Date) AND ([Active] = @Original_Active))' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameDeviceSelectCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameDeviceSelectCommand]
AS
	SET NOCOUNT ON;
select * from gamedevices' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameDeviceInsertCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameDeviceInsertCommand]
(
	@pkGameDevice uniqueidentifier,
	@idGame uniqueidentifier,
	@idLastState uniqueidentifier,
	@Device nvarchar(50),
	@User nvarchar(50),
	@Latitude float,
	@Longitude float,
	@Accepted bit,
	@Ready bit,
	@Date datetime
)
AS
	SET NOCOUNT OFF;
INSERT INTO [gamedevices] ([pkGameDevice], [idGame], [idLastState], [Device], [User], [Latitude], [Longitude], [Accepted], [Ready], [Date]) VALUES (@pkGameDevice, @idGame, @idLastState, @Device, @User, @Latitude, @Longitude, @Accepted, @Ready, @Date);
	
SELECT pkGameDevice, idGame, idLastState, Device, [User], Latitude, Longitude, Accepted, Ready, Date FROM gameDevices WHERE (pkGameDevice = @pkGameDevice)' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameDeviceUpdateCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameDeviceUpdateCommand]
(
	@pkGameDevice uniqueidentifier,
	@idGame uniqueidentifier,
	@idLastState uniqueidentifier,
	@Device nvarchar(50),
	@User nvarchar(50),
	@Latitude float,
	@Longitude float,
	@Accepted bit,
	@Ready bit,
	@Date datetime,
	@Original_pkGameDevice uniqueidentifier,
	@Original_idGame uniqueidentifier,
	@Original_idLastState uniqueidentifier,
	@Original_Device nvarchar(50),
	@Original_User nvarchar(50),
	@Original_Latitude float,
	@Original_Longitude float,
	@Original_Accepted bit,
	@Original_Ready bit,
	@Original_Date datetime
)
AS
	SET NOCOUNT OFF;
UPDATE [gamedevices] SET [pkGameDevice] = @pkGameDevice, [idGame] = @idGame, [idLastState] = @idLastState, [Device] = @Device, [User] = @User, [Latitude] = @Latitude, [Longitude] = @Longitude, [Accepted] = @Accepted, [Ready] = @Ready, [Date] = @Date WHERE (([pkGameDevice] = @Original_pkGameDevice) AND ([idGame] = @Original_idGame) AND ([idLastState] = @Original_idLastState) AND ([Device] = @Original_Device) AND ([User] = @Original_User) AND ([Latitude] = @Original_Latitude) AND ([Longitude] = @Original_Longitude) AND ([Accepted] = @Original_Accepted) AND ([Ready] = @Original_Ready) AND ([Date] = @Original_Date));
	
SELECT pkGameDevice, idGame, idLastState, Device, [User], Latitude, Longitude, Accepted, Ready, Date FROM gameDevices WHERE (pkGameDevice = @pkGameDevice)' 
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GameDeviceDeleteCommand]') AND type in (N'P', N'PC'))
BEGIN
EXEC dbo.sp_executesql @statement = N'CREATE PROCEDURE [dbo].[GameDeviceDeleteCommand]
(
	@Original_pkGameDevice uniqueidentifier,
	@Original_idGame uniqueidentifier,
	@Original_idLastState uniqueidentifier,
	@Original_Device nvarchar(50),
	@Original_User nvarchar(50),
	@Original_Latitude float,
	@Original_Longitude float,
	@Original_Accepted bit,
	@Original_Ready bit,
	@Original_Date datetime
)
AS
	SET NOCOUNT OFF;
DELETE FROM [gamedevices] WHERE (([pkGameDevice] = @Original_pkGameDevice) AND ([idGame] = @Original_idGame) AND ([idLastState] = @Original_idLastState) AND ([Device] = @Original_Device) AND ([User] = @Original_User) AND ([Latitude] = @Original_Latitude) AND ([Longitude] = @Original_Longitude) AND ([Accepted] = @Original_Accepted) AND ([Ready] = @Original_Ready) AND ([Date] = @Original_Date))' 
END
GO
USE [GameServer]
GO
USE [GameServer]
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_gameStates_games]') AND parent_object_id = OBJECT_ID(N'[dbo].[gameStates]'))
ALTER TABLE [dbo].[gameStates]  WITH CHECK ADD  CONSTRAINT [FK_gameStates_games] FOREIGN KEY([idGame])
REFERENCES [dbo].[games] ([pkGame])
ON DELETE CASCADE
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_gameDevices_games]') AND parent_object_id = OBJECT_ID(N'[dbo].[gameDevices]'))
ALTER TABLE [dbo].[gameDevices]  WITH CHECK ADD  CONSTRAINT [FK_gameDevices_games] FOREIGN KEY([idGame])
REFERENCES [dbo].[games] ([pkGame])
ON UPDATE CASCADE
ON DELETE CASCADE
