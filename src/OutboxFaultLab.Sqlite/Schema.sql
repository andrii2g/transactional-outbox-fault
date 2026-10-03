PRAGMA foreign_keys = ON;
CREATE TABLE Orders (
    OrderId TEXT NOT NULL PRIMARY KEY,
    AmountCents INTEGER NOT NULL CHECK (AmountCents >= 0)
);
CREATE TABLE Outbox (
    Sequence INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId TEXT NOT NULL UNIQUE,
    OrderId TEXT NOT NULL UNIQUE,
    PayloadJson TEXT NOT NULL,
    CreatedTick INTEGER NOT NULL CHECK (CreatedTick >= 0),
    SentTick INTEGER NULL CHECK (SentTick IS NULL OR SentTick >= CreatedTick),
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId)
);
CREATE INDEX IX_Outbox_Unsent ON Outbox(Sequence) WHERE SentTick IS NULL;
