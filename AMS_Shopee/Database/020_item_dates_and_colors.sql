-- 020_item_dates_and_colors.sql
-- This adds two things:
--   MFD / EXP can be month and year only ("EXP 03/2027") or a full date.
--   I save a month-only EXP as the last day of that month, and a month-only MFD as the 1st.
--   Supply items can have colors (Black, Blue, Red...). Offices choose how many of each color
--   in e-SRS, and the colors are written on the RIS line.
--
-- Run once in phpMyAdmin (database db64557) BEFORE publishing e-PINS and e-SRS.
-- If a line says "#1060 Duplicate column", that step is already done, so run the rest.

ALTER TABLE supply_items ADD COLUMN mfd_month_only TINYINT(1) NOT NULL DEFAULT 0 AFTER expires_on;
ALTER TABLE supply_items ADD COLUMN exp_month_only TINYINT(1) NOT NULL DEFAULT 0 AFTER mfd_month_only;
ALTER TABLE supply_items ADD COLUMN color_options VARCHAR(500) NULL AFTER exp_month_only;
ALTER TABLE cart_items ADD COLUMN colors VARCHAR(500) NULL AFTER quantity;

INSERT IGNORE INTO schema_migrations (version) VALUES ('020_item_dates_and_colors');
