SET IDENTITY_INSERT [dbo].[Bookings] ON

-- Booking 22: A recent booking for Package 2 (Confirmed)
INSERT INTO [dbo].[Bookings] ([BookingID], [UserID], [PackageID], [BookingDate], [TravelDate], [NumberOfPeople], [TotalBeforeDiscount], [TotalDiscountAmount], [FinalAmount], [BookingStatus]) 
VALUES (22, 3, 2, N'2025-12-19 09:00:00', N'2026-01-15 00:00:00', 2, CAST(3000.00 AS Decimal(18, 2)), CAST(150.00 AS Decimal(18, 2)), CAST(2850.00 AS Decimal(18, 2)), N'Confirmed')

-- Booking 23: A pending booking for Package 1
INSERT INTO [dbo].[Bookings] ([BookingID], [UserID], [PackageID], [BookingDate], [TravelDate], [NumberOfPeople], [TotalBeforeDiscount], [TotalDiscountAmount], [FinalAmount], [BookingStatus]) 
VALUES (23, 3, 1, N'2025-12-20 14:30:00', N'2026-02-01 00:00:00', 1, CAST(1200.00 AS Decimal(18, 2)), CAST(0.00 AS Decimal(18, 2)), CAST(1200.00 AS Decimal(18, 2)), N'Pending')

-- Booking 24: A completed booking for Package 3
INSERT INTO [dbo].[Bookings] ([BookingID], [UserID], [PackageID], [BookingDate], [TravelDate], [NumberOfPeople], [TotalBeforeDiscount], [TotalDiscountAmount], [FinalAmount], [BookingStatus]) 
VALUES (24, 3, 3, N'2025-11-01 10:15:00', N'2025-11-20 00:00:00', 4, CAST(2000.00 AS Decimal(18, 2)), CAST(200.00 AS Decimal(18, 2)), CAST(1800.00 AS Decimal(18, 2)), N'Completed')

SET IDENTITY_INSERT [dbo].[Bookings] OFF