/****** Object:  Table [dbo].[Feeds]    Script Date: 02/25/2012 17:45:44 ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Feeds]') AND type in (N'U'))
DROP TABLE [dbo].[Feeds]
GO
/****** Object:  Default [DF_Feeds_PortalId]    Script Date: 02/25/2012 17:45:45 ******/
IF  EXISTS (SELECT * FROM sys.default_constraints WHERE object_id = OBJECT_ID(N'[dbo].[DF_Feeds_PortalId]') AND parent_object_id = OBJECT_ID(N'[dbo].[Feeds]'))
Begin
IF  EXISTS (SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'[DF_Feeds_PortalId]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Feeds] DROP CONSTRAINT [DF_Feeds_PortalId]
END


End
GO
/****** Object:  Table [dbo].[Feeds]    Script Date: 02/25/2012 17:45:44 ******/
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
	[PortalId] [int] NOT NULL,
	[Category] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_Feeds] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON)
)
END
GO
SET IDENTITY_INSERT [dbo].[Feeds] ON
INSERT [dbo].[Feeds] ([Id], [Name], [URL], [LastRun], [Status], [PortalId], [Category]) VALUES (1, N'Hotels', N'http://xmlfeed.laterooms.com/staticdata/hotels_standard.zip', NULL, N'Cancel', 0, N'Hotels')
INSERT [dbo].[Feeds] ([Id], [Name], [URL], [LastRun], [Status], [PortalId], [Category]) VALUES (2, N'Trade Doubler', N'http://pf.tradedoubler.com/export/export?myFeed=13267926451624184&myFormat=12196865151077321', NULL, N'Error', 0, NULL)
SET IDENTITY_INSERT [dbo].[Feeds] OFF
/****** Object:  Default [DF_Feeds_PortalId]    Script Date: 02/25/2012 17:45:45 ******/
IF Not EXISTS (SELECT * FROM sys.default_constraints WHERE object_id = OBJECT_ID(N'[dbo].[DF_Feeds_PortalId]') AND parent_object_id = OBJECT_ID(N'[dbo].[Feeds]'))
Begin
IF NOT EXISTS (SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'[DF_Feeds_PortalId]') AND type = 'D')
BEGIN
ALTER TABLE [dbo].[Feeds] ADD  CONSTRAINT [DF_Feeds_PortalId]  DEFAULT ((0)) FOR [PortalId]
END


End
GO
