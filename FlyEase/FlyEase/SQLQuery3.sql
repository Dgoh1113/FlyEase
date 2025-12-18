SET IDENTITY_INSERT [dbo].[Bookings] ON

-- Booking 25: Completed trip to Package 1 (Travelled last month)
INSERT INTO [dbo].[Bookings] ([BookingID], [UserID], [PackageID], [BookingDate], [TravelDate], [NumberOfPeople], [TotalBeforeDiscount], [TotalDiscountAmount], [FinalAmount], [BookingStatus]) 
VALUES (25, 3, 1, N'2025-10-01 10:00:00', N'2025-11-15 00:00:00', 2, CAST(2400.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(2400.00 AS Decimal(18, 2)), N'Completed')

-- Booking 26: Completed trip to Package 3 (Travelled in October)
INSERT INTO [dbo].[Bookings] ([BookingID], [UserID], [PackageID], [BookingDate], [TravelDate], [NumberOfPeople], [TotalBeforeDiscount], [TotalDiscountAmount], [FinalAmount], [BookingStatus]) 
VALUES (26, 3, 3, N'2025-09-15 14:20:00', N'2025-10-10 00:00:00', 1, CAST(500.00 AS Decimal(18, 2)), CAST(50.00 AS Decimal(18, 2)), CAST(450.00 AS Decimal(18, 2)), N'Completed')

-- Booking 27: Completed trip to Package 2 (Travelled in September)
INSERT INTO [dbo].[Bookings] ([BookingID], [UserID], [PackageID], [BookingDate], [TravelDate], [NumberOfPeople], [TotalBeforeDiscount], [TotalDiscountAmount], [FinalAmount], [BookingStatus]) 
VALUES (27, 3, 2, N'2025-08-20 09:45:00', N'2025-09-25 00:00:00', 2, CAST(3000.00 AS Decimal(18, 2)), CAST(200.00 AS Decimal(18, 2)), CAST(2800.00 AS Decimal(18, 2)), N'Completed')

SET IDENTITY_INSERT [dbo].[Bookings] OFF