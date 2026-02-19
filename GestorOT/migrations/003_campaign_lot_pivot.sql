-- Migration: CampaignLot Pivot Architecture
-- Purpose: Make CampaignPlot the primary operational context for Labors
-- Run this on Supabase SQL Editor

-- 1. Add CadastralSurfaceHa column to Lots (physical/cadastral surface)
ALTER TABLE public."Lots"
ADD COLUMN IF NOT EXISTS "CadastralSurfaceHa" NUMERIC(10,2) NOT NULL DEFAULT 0;

-- 2. Backfill CadastralSurfaceHa from geometry area (PostGIS)
UPDATE public."Lots"
SET "CadastralSurfaceHa" = ROUND(
    CAST(ST_Area(ST_Transform("Geometry", 32720)) / 10000.0 AS NUMERIC), 2
)
WHERE "Geometry" IS NOT NULL AND "CadastralSurfaceHa" = 0;

-- 3. Make Labor.LotId nullable (was required, now optional since CampaignPlotId is primary)
ALTER TABLE public."Labors"
ALTER COLUMN "LotId" DROP NOT NULL;

-- 4. Ensure CampaignPlotId column exists on Labors (should already exist)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Labors' AND column_name = 'CampaignPlotId'
    ) THEN
        ALTER TABLE public."Labors"
        ADD COLUMN "CampaignPlotId" UUID REFERENCES public."CampaignPlots"("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- 5. Backfill: For labors that have LotId + a campaign context, resolve CampaignPlotId
UPDATE public."Labors" l
SET "CampaignPlotId" = cp."Id"
FROM public."CampaignPlots" cp
WHERE l."LotId" = cp."PlotId"
  AND l."CampaignPlotId" IS NULL
  AND cp."CampaignId" = (
      SELECT c."Id" FROM public."Campaigns" c
      WHERE c."IsActive" = true
      ORDER BY c."CreatedAt" DESC
      LIMIT 1
  );

-- 6. Also backfill via WorkOrder campaign
UPDATE public."Labors" l
SET "CampaignPlotId" = cp."Id"
FROM public."WorkOrders" wo, public."CampaignPlots" cp
WHERE l."WorkOrderId" = wo."Id"
  AND l."LotId" = cp."PlotId"
  AND wo."CampaignId" = cp."CampaignId"
  AND l."CampaignPlotId" IS NULL;

-- 7. Add index on CampaignPlotId for performance
CREATE INDEX IF NOT EXISTS "IX_Labors_CampaignPlotId" ON public."Labors" ("CampaignPlotId");

-- 8. Verify unique constraint on CampaignPlots (CampaignId, PlotId)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CampaignPlots_CampaignId_PlotId"
ON public."CampaignPlots" ("CampaignId", "PlotId");
