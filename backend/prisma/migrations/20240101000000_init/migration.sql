-- CreateEnum
CREATE TYPE "Region" AS ENUM ('INDIA', 'GLOBAL');

-- CreateTable
CREATE TABLE "User" (
    "id"           TEXT NOT NULL,
    "email"        TEXT NOT NULL,
    "name"         TEXT,
    "passwordHash" TEXT,
    "createdAt"    TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "FundMeta" (
    "id"         TEXT NOT NULL,
    "region"     "Region" NOT NULL,
    "name"       TEXT NOT NULL,
    "amc"        TEXT NOT NULL,
    "ticker"     TEXT NOT NULL,
    "schemeCode" TEXT,
    "isin"       TEXT,
    "category"   TEXT,
    "updatedAt"  TIMESTAMP(3) NOT NULL,
    "latestNav"  DECIMAL(65,30),
    "navDate"    TIMESTAMP(3),
    CONSTRAINT "FundMeta_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Holding" (
    "id"         TEXT NOT NULL,
    "userId"     TEXT NOT NULL,
    "fundId"     TEXT NOT NULL,
    "units"      DECIMAL(65,30) NOT NULL,
    "avgCost"    DECIMAL(65,30),
    "purchaseAt" TIMESTAMP(3) NOT NULL,
    "createdAt"  TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt"  TIMESTAMP(3) NOT NULL,
    CONSTRAINT "Holding_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "NavHistory" (
    "id"      TEXT NOT NULL,
    "fundId"  TEXT NOT NULL,
    "nav"     DECIMAL(65,30) NOT NULL,
    "navDate" DATE NOT NULL,
    CONSTRAINT "NavHistory_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "User_email_key" ON "User"("email");
CREATE UNIQUE INDEX "FundMeta_ticker_key" ON "FundMeta"("ticker");
CREATE UNIQUE INDEX "Holding_userId_fundId_key" ON "Holding"("userId", "fundId");
CREATE UNIQUE INDEX "NavHistory_fundId_navDate_key" ON "NavHistory"("fundId", "navDate");

-- CreateIndex (non-unique)
CREATE INDEX "FundMeta_region_idx" ON "FundMeta"("region");
CREATE INDEX "FundMeta_name_idx"   ON "FundMeta"("name");
CREATE INDEX "FundMeta_amc_idx"    ON "FundMeta"("amc");
CREATE INDEX "Holding_userId_idx"  ON "Holding"("userId");
CREATE INDEX "NavHistory_fundId_navDate_idx" ON "NavHistory"("fundId", "navDate");

-- AddForeignKey
ALTER TABLE "Holding" ADD CONSTRAINT "Holding_userId_fkey"
    FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE "Holding" ADD CONSTRAINT "Holding_fundId_fkey"
    FOREIGN KEY ("fundId") REFERENCES "FundMeta"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

ALTER TABLE "NavHistory" ADD CONSTRAINT "NavHistory_fundId_fkey"
    FOREIGN KEY ("fundId") REFERENCES "FundMeta"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
