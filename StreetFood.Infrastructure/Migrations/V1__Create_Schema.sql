CREATE TABLE Languages (
    Code VARCHAR(5) PRIMARY KEY,
    Name VARCHAR(50) NOT NULL
);

CREATE TABLE Users (
    Id SERIAL PRIMARY KEY,
    Username VARCHAR(50) UNIQUE NOT NULL,
    Password TEXT NOT NULL,
    Role VARCHAR(20) NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE POIs (
    Id SERIAL PRIMARY KEY,
    Latitude DOUBLE PRECISION NOT NULL,
    Longitude DOUBLE PRECISION NOT NULL,
    Radius INT DEFAULT 5,
    Address TEXT,
    ImageUrl TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE POI_Translations (
    Id SERIAL PRIMARY KEY,
    PoiId INT NOT NULL,
    LanguageCode VARCHAR(5) NOT NULL,
    Name VARCHAR(255) NOT NULL,
    Description TEXT,

    FOREIGN KEY (PoiId) REFERENCES POIs(Id) ON DELETE CASCADE,
    FOREIGN KEY (LanguageCode) REFERENCES Languages(Code)
);

CREATE UNIQUE INDEX idx_poi_language
ON POI_Translations(PoiId, LanguageCode);

CREATE TABLE Restaurant_Details (
    Id SERIAL PRIMARY KEY,
    PoiId INT UNIQUE,
    OpeningHours VARCHAR(100),
    Phone VARCHAR(20),

    FOREIGN KEY (PoiId) REFERENCES POIs(Id) ON DELETE CASCADE
);

CREATE TABLE Restaurant_Owners (
    Id SERIAL PRIMARY KEY,
    UserId INT,
    PoiId INT,

    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (PoiId) REFERENCES POIs(Id)
);

CREATE TABLE Foods (
    Id SERIAL PRIMARY KEY,
    PoiId INT NOT NULL,
    Name VARCHAR(255) NOT NULL,
    Description TEXT,
    Price INT,
    ImageUrl TEXT,

    FOREIGN KEY (PoiId) REFERENCES POIs(Id) ON DELETE CASCADE
);

CREATE TABLE Restaurant_Audio (
    Id SERIAL PRIMARY KEY,
    PoiId INT NOT NULL,
    LanguageCode VARCHAR(5) NOT NULL,
    AudioUrl TEXT NOT NULL,

    FOREIGN KEY (PoiId) REFERENCES POIs(Id) ON DELETE CASCADE,
    FOREIGN KEY (LanguageCode) REFERENCES Languages(Code)
);

CREATE UNIQUE INDEX idx_audio_language
ON Restaurant_Audio(PoiId, LanguageCode);

CREATE TABLE Script_Change_Requests (
    Id SERIAL PRIMARY KEY,
    PoiId INT,
    LanguageCode VARCHAR(5),
    NewScript TEXT,
    Status VARCHAR(20) DEFAULT 'pending',
    CreatedBy INT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (PoiId) REFERENCES POIs(Id),
    FOREIGN KEY (CreatedBy) REFERENCES Users(Id)
);

CREATE TABLE Device_Visits (
    Id SERIAL PRIMARY KEY,
    DeviceId VARCHAR(100),
    PoiId INT,
    EnterTime TIMESTAMP,
    ExitTime TIMESTAMP,
    Duration INT,

    FOREIGN KEY (PoiId) REFERENCES POIs(Id)
);

CREATE TABLE Location_Logs (
    Id SERIAL PRIMARY KEY,
    DeviceId VARCHAR(100),
    Latitude DOUBLE PRECISION,
    Longitude DOUBLE PRECISION,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Movement_Paths (
    Id SERIAL PRIMARY KEY,
    DeviceId VARCHAR(100),
    FromPoiId INT,
    ToPoiId INT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);