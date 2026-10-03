PRAGMA foreign_keys = ON;
CREATE TABLE ProcessedEvents (
    EventId TEXT NOT NULL PRIMARY KEY,
    OrderId TEXT NOT NULL,
    PayloadHash TEXT NOT NULL,
    ProcessedTick INTEGER NOT NULL CHECK (ProcessedTick >= 0)
);
CREATE TABLE Projection (
    OrderId TEXT NOT NULL PRIMARY KEY,
    AppliedCount INTEGER NOT NULL CHECK (AppliedCount >= 1),
    AppliedAmountCents INTEGER NOT NULL CHECK (AppliedAmountCents >= 0)
);
