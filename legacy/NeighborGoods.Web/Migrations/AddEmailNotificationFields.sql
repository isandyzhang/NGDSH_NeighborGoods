-- Migration: Add Email Notification Fields
-- 執行日期: 2025-01-01
-- 說明: 新增 Email 通知相關欄位到 AspNetUsers 表

-- 新增 EmailNotificationEnabled 欄位
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'EmailNotificationEnabled')
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [EmailNotificationEnabled] BIT NOT NULL DEFAULT 0;
    PRINT '已新增 EmailNotificationEnabled 欄位';
END
ELSE
BEGIN
    PRINT 'EmailNotificationEnabled 欄位已存在';
END
GO

-- 新增 EmailNotificationLastSentAt 欄位
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'EmailNotificationLastSentAt')
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [EmailNotificationLastSentAt] DATETIME2 NULL;
    PRINT '已新增 EmailNotificationLastSentAt 欄位';
END
ELSE
BEGIN
    PRINT 'EmailNotificationLastSentAt 欄位已存在';
END
GO

PRINT 'Migration 完成：Email 通知欄位已新增';
