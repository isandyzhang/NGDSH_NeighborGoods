-- 插入20筆測試商品資料
-- 使用說明：
-- 1. 請先查詢現有用戶ID：SELECT Id FROM AspNetUsers;
-- 2. 將下方 SQL 中的 'YOUR_USER_ID_1', 'YOUR_USER_ID_2' 等替換為實際的用戶ID
-- 3. 如果只有一個用戶，可以將所有 SellerId 都設為同一個ID

-- 根據用戶名稱自動取得用戶ID（如果用戶名稱不存在，會使用第一個找到的用戶）
DECLARE @UserId1 NVARCHAR(450);
DECLARE @UserId2 NVARCHAR(450);

-- 根據用戶名稱查詢ID，如果找不到則使用第一個用戶
SELECT @UserId1 = Id FROM AspNetUsers WHERE UserName = 'AndyTest1';
IF @UserId1 IS NULL
    SELECT TOP 1 @UserId1 = Id FROM AspNetUsers ORDER BY CreatedAt;

SELECT @UserId2 = Id FROM AspNetUsers WHERE UserName = 'paul';
IF @UserId2 IS NULL
    SELECT TOP 1 @UserId2 = Id FROM AspNetUsers ORDER BY CreatedAt DESC;

-- 如果只有一個用戶，使用同一個ID
IF @UserId1 = @UserId2 OR @UserId2 IS NULL
    SET @UserId2 = @UserId1;

-- 檢查是否有用戶
IF @UserId1 IS NULL
BEGIN
    PRINT '錯誤：資料庫中沒有用戶，請先建立用戶帳號！';
    RETURN;
END

PRINT '使用用戶ID: ' + @UserId1;
IF @UserId1 != @UserId2
    PRINT '使用用戶ID: ' + @UserId2;

-- 插入20筆商品資料
INSERT INTO Listings (Id, Title, Description, Price, IsFree, IsCharity, Status, Category, Condition, PickupLocation, SellerId, CreatedAt, UpdatedAt)
VALUES
-- 1. 家具家飾 - 全新
(NEWID(), N'IKEA 白色書桌', N'全新未拆封的IKEA書桌，尺寸120x60cm，適合小空間使用。因為搬家多買了一個，原價2990元。', 2000, 0, 0, 0, 0, 0, 0, @UserId1, GETDATE(), GETDATE()),

-- 2. 電子產品 - 近全新
(NEWID(), N'Apple iPad Air 第4代', N'近全新iPad Air，使用不到3個月，有保護貼和保護殼。盒裝完整，配件齊全。', 12000, 0, 0, 0, 1, 1, 1, @UserId2, GETDATE(), GETDATE()),

-- 3. 服飾配件 - 良好
(NEWID(), N'Uniqlo 羽絨外套', N'九成新羽絨外套，M號，只穿過幾次，保暖效果佳。', 800, 0, 0, 0, 2, 2, 2, @UserId1, GETDATE(), GETDATE()),

-- 4. 書籍文具 - 免費
(NEWID(), N'大學教科書 - 微積分', N'大學微積分課本，已用不到，免費贈送給需要的同學。', 0, 1, 0, 0, 3, 3, 3, @UserId2, GETDATE(), GETDATE()),

-- 5. 運動用品 - 良好
(NEWID(), N'籃球', N'使用過的籃球，狀況良好，適合日常練習使用。', 300, 0, 0, 0, 4, 2, 0, @UserId1, GETDATE(), GETDATE()),

-- 6. 玩具遊戲 - 近全新
(NEWID(), N'樂高積木組', N'近全新的樂高積木組，適合6-12歲兒童，盒裝完整。', 1500, 0, 0, 0, 5, 1, 1, @UserId2, GETDATE(), GETDATE()),

-- 7. 廚房用品 - 普通
(NEWID(), N'不鏽鋼鍋具組', N'三件式不鏽鋼鍋具組，使用過但功能正常，適合租屋族。', 500, 0, 0, 0, 6, 3, 2, @UserId1, GETDATE(), GETDATE()),

-- 8. 生活用品 - 愛心商品
(NEWID(), N'全新毛巾組', N'全新未拆封的毛巾組，共6條，愛心商品免費贈送。', 0, 1, 1, 0, 7, 0, 0, @UserId2, GETDATE(), GETDATE()),

-- 9. 嬰幼兒用品 - 良好
(NEWID(), N'嬰兒推車', N'使用過的嬰兒推車，狀況良好，可摺疊收納，適合0-3歲。', 2000, 0, 0, 0, 8, 2, 1, @UserId1, GETDATE(), GETDATE()),

-- 10. 電子產品 - 保留中
(NEWID(), N'MacBook Pro 13吋', N'2019年MacBook Pro，使用狀況良好，已升級SSD。', 25000, 0, 0, 1, 1, 2, 3, @UserId2, GETDATE(), GETDATE()),

-- 11. 家具家飾 - 已售出
(NEWID(), N'沙發床', N'可變形沙發床，適合小空間，已售出。', 3500, 0, 0, 2, 0, 3, 0, @UserId1, GETDATE(), GETDATE()),

-- 12. 服飾配件 - 歲月痕跡
(NEWID(), N'二手牛仔褲', N'有使用痕跡的牛仔褲，L號，適合當工作褲。', 200, 0, 0, 0, 2, 4, 2, @UserId2, GETDATE(), GETDATE()),

-- 13. 書籍文具 - 良好
(NEWID(), N'英文小說集', N'多本英文小說，適合練習英文閱讀，狀況良好。', 600, 0, 0, 0, 3, 2, 1, @UserId1, GETDATE(), GETDATE()),

-- 14. 運動用品 - 已捐贈
(NEWID(), N'瑜伽墊', N'使用過的瑜伽墊，已捐贈給社區中心。', 0, 1, 1, 3, 4, 3, 0, @UserId2, GETDATE(), GETDATE()),

-- 15. 廚房用品 - 全新
(NEWID(), N'電磁爐', N'全新未拆封的電磁爐，適合租屋族使用，原價1990元。', 1500, 0, 0, 0, 6, 0, 1, @UserId1, GETDATE(), GETDATE()),

-- 16. 生活用品 - 近全新
(NEWID(), N'收納箱組', N'近全新的收納箱組，共4個，適合整理房間使用。', 800, 0, 0, 0, 7, 1, 2, @UserId2, GETDATE(), GETDATE()),

-- 17. 電子產品 - 普通
(NEWID(), N'無線滑鼠', N'使用過的無線滑鼠，功能正常，適合備用。', 200, 0, 0, 0, 1, 3, 3, @UserId1, GETDATE(), GETDATE()),

-- 18. 玩具遊戲 - 良好
(NEWID(), N'拼圖遊戲', N'1000片拼圖，適合親子活動，盒裝完整。', 400, 0, 0, 0, 5, 2, 0, @UserId2, GETDATE(), GETDATE()),

-- 19. 其他 - 已下架
(NEWID(), N'雜物箱', N'各種小雜物，已下架。', 100, 0, 0, 4, 9, 3, 1, @UserId1, GETDATE(), GETDATE()),

-- 20. 家具家飾 - 良好
(NEWID(), N'書櫃', N'三層書櫃，狀況良好，適合放置書籍和雜物。', 1200, 0, 0, 0, 0, 2, 2, @UserId2, GETDATE(), GETDATE());

PRINT '成功插入20筆商品資料！';

-- 查詢結果確認
SELECT COUNT(*) AS TotalListings FROM Listings;
SELECT TOP 20 Id, Title, Status, Category, Condition, Price, IsFree, IsCharity, SellerId, CreatedAt 
FROM Listings 
ORDER BY CreatedAt DESC;

