/****** Object:  Table [dbo].[Feeds]    Script Date: 02/05/2012 18:01:57 ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Feeds]') AND type in (N'U'))
DROP TABLE [dbo].[Feeds]
GO
/****** Object:  Table [dbo].[Feeds]    Script Date: 02/05/2012 18:01:57 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Feeds]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Feeds](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[URL] [nvarchar](250) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[LastRun] [datetime] NULL,
	[Status] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_Feeds] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON)
)
END
GO
SET IDENTITY_INSERT [dbo].[Feeds] ON
INSERT [dbo].[Feeds] ([Id], [Name], [URL], [LastRun], [Status]) VALUES (1, N'Hotels', N'http://xmlfeed.laterooms.com/staticdata/hotels_standard.zip', CAST(0x00009FEE00D0D78B AS DateTime), N'Error')
INSERT [dbo].[Feeds] ([Id], [Name], [URL], [LastRun], [Status]) VALUES (2, N'Trade Doubler', N'http://pf.tradedoubler.com/export/export?myFeed=13267926451624184&myFormat=12196865151077321', CAST(0x00009FEE00CA6341 AS DateTime), N'Success')
SET IDENTITY_INSERT [dbo].[Feeds] OFF
