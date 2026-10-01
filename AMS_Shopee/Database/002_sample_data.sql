-- =====================================================================
-- ams_stockwatch :: 002 — SAMPLE DATA for development and testing
-- Run in MySQL Workbench AFTER 001. Safe to run twice (INSERT IGNORE).
-- Prices and quantities are illustrative only; replace with real data
-- from AMS before going live.
--
-- Office logins created here:
--   username = office acronym in lowercase (ord, oard, legal, ictu, clmd-lrms, ...)
--   password = Supply@2026   (flagged "must change" for first login)
-- =====================================================================

USE ams_stockwatch;
SET NAMES utf8mb4;
SET @fy := YEAR(CURDATE());   -- APP-CSE year = current year

-- ---------------------------------------------------------------------
-- Categories (icon = Bootstrap Icons class shown on cards without photos)
-- ---------------------------------------------------------------------
INSERT IGNORE INTO item_categories (category_name, icon, sort_order) VALUES
 ('Paper products',        'bi-file-earmark-text', 1),
 ('Writing instruments',   'bi-pen',               2),
 ('Filing and storage',    'bi-folder2-open',      3),
 ('Desk supplies',         'bi-paperclip',         4),
 ('Printer consumables',   'bi-printer',           5),
 ('Cleaning and janitorial','bi-droplet',          6);

INSERT IGNORE INTO suppliers (supplier_name) VALUES ('Procurement Service - DBM (PS-DBM)');

-- ---------------------------------------------------------------------
-- Items. Stock levels are chosen so every badge colour appears:
-- green = above reorder level, yellow = at/below reorder level, red = 0.
-- sample_alloc = APP-CSE quantity given to EVERY office for this item
-- (0 = not in anyone's APP-CSE, to test the "Not in your APP-CSE" message).
-- ---------------------------------------------------------------------
DROP TEMPORARY TABLE IF EXISTS seed_items;
CREATE TEMPORARY TABLE seed_items (
  stock_no VARCHAR(30), item_name VARCHAR(200), specifications TEXT, unit_of_measure VARCHAR(30),
  category VARCHAR(100), unit_price DECIMAL(12,2), stock_on_hand INT, reorder_level INT, sample_alloc INT
) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
INSERT INTO seed_items VALUES
 ('SN-0001','Bond paper, A4','70 gsm, 500 sheets per ream','ream','Paper products',245.00,180,40,60),
 ('SN-0002','Bond paper, legal','8.5" x 13", 70 gsm, 500 sheets per ream','ream','Paper products',265.00,25,40,40),
 ('SN-0003','Sticky notes','3" x 3", 100 sheets per pad, assorted colours','pad','Paper products',28.00,0,20,24),
 ('SN-0004','Record book','300 pages, hardbound','book','Paper products',95.00,60,15,6),
 ('SN-0101','Ballpen, black','0.5 mm, retractable','pc','Writing instruments',8.50,600,100,50),
 ('SN-0102','Ballpen, blue','0.5 mm, retractable','pc','Writing instruments',8.50,80,100,50),
 ('SN-0103','Sign pen, black','0.5 mm, liquid ink','pc','Writing instruments',35.00,120,30,12),
 ('SN-0104','Highlighter, yellow','Chisel tip, fluorescent ink','pc','Writing instruments',22.00,0,20,12),
 ('SN-0201','Folder, long, white','11.5" x 14", 100 pieces per pack','pack','Filing and storage',310.00,35,10,4),
 ('SN-0202','Envelope, expanding, long','Kraft board with gusset','pc','Filing and storage',18.00,250,50,30),
 ('SN-0203','Fastener, plastic','50 sets per box','box','Filing and storage',85.00,8,10,6),
 ('SN-0301','Paper clip, vinyl-coated','33 mm, 100 pieces per box','box','Desk supplies',24.00,90,20,12),
 ('SN-0302','Staple wire, standard','No. 35, 5,000 pieces per box','box','Desk supplies',38.00,70,20,10),
 ('SN-0303','Correction tape','5 mm x 6 m','pc','Desk supplies',32.00,40,10,0),
 ('SN-0401','Ink refill, black','70 ml bottle, for ink-tank printers','bottle','Printer consumables',295.00,15,10,6),
 ('SN-0402','Toner cartridge, black','Laser printer, high yield','cartridge','Printer consumables',3450.00,0,2,2),
 ('SN-0501','Ethyl alcohol, 70%','500 ml bottle','bottle','Cleaning and janitorial',65.00,140,30,24),
 ('SN-0502','Toilet tissue, 2-ply','12 rolls per pack','pack','Cleaning and janitorial',145.00,30,10,12);

INSERT IGNORE INTO supply_items
  (stock_no, item_name, specifications, unit_of_measure, category_id, supplier_id,
   unit_price, stock_on_hand, reorder_level)
SELECT s.stock_no, s.item_name, s.specifications, s.unit_of_measure, c.category_id,
       (SELECT supplier_id FROM suppliers WHERE supplier_name = 'Procurement Service - DBM (PS-DBM)'),
       s.unit_price, s.stock_on_hand, s.reorder_level
  FROM seed_items s
  JOIN item_categories c ON c.category_name = s.category;

-- Opening balances in the stock ledger (only for items that have none yet)
INSERT INTO stock_movements (item_id, movement_type, quantity, balance_after, unit_cost,
                             reference_type, reference_no, remarks, performed_by)
SELECT i.item_id, 'OPENING', i.stock_on_hand, i.stock_on_hand, i.unit_price,
       'ADJ', 'SAMPLE-OPENING', 'Sample data opening balance',
       (SELECT MIN(user_id) FROM users WHERE role = 'SuperAdmin')
  FROM supply_items i
  JOIN seed_items s ON s.stock_no = i.stock_no
 WHERE i.stock_on_hand > 0
   AND NOT EXISTS (SELECT 1 FROM stock_movements m WHERE m.item_id = i.item_id);

-- ---------------------------------------------------------------------
-- APP-CSE allocations: every requisitioning office x every allocated item
-- ---------------------------------------------------------------------
INSERT IGNORE INTO app_cse_allocations (office_id, item_id, fiscal_year, allocated_qty,
                                        q1_qty, q2_qty, q3_qty, q4_qty)
SELECT o.office_id, i.item_id, @fy, s.sample_alloc,
       CEIL(s.sample_alloc / 4), CEIL(s.sample_alloc / 4), CEIL(s.sample_alloc / 4),
       s.sample_alloc - 3 * CEIL(s.sample_alloc / 4)
  FROM offices o
  CROSS JOIN supply_items i
  JOIN seed_items s ON s.stock_no = i.stock_no
 WHERE o.can_requisition = 1
   AND s.sample_alloc > 0;

-- A few realistic situations to test the quota banner:
--   ORD has already been issued 20 of its 50 black ballpens  → Remaining 30
--   ICTU has used up all 50 black ballpens                   → Remaining 0
UPDATE app_cse_allocations a
  JOIN offices o      ON o.office_id = a.office_id
  JOIN supply_items i ON i.item_id   = a.item_id
   SET a.issued_qty = 20
 WHERE o.office_acronym = 'ORD' AND i.stock_no = 'SN-0101' AND a.fiscal_year = @fy AND a.issued_qty = 0;

UPDATE app_cse_allocations a
  JOIN offices o      ON o.office_id = a.office_id
  JOIN supply_items i ON i.item_id   = a.item_id
   SET a.issued_qty = a.allocated_qty
 WHERE o.office_acronym = 'ICTU' AND i.stock_no = 'SN-0101' AND a.fiscal_year = @fy AND a.issued_qty = 0;

-- ---------------------------------------------------------------------
-- One login per requisitioning office (AMS keeps using its admin account)
-- Password: Supply@2026  (bcrypt, cost 12)
-- ---------------------------------------------------------------------
INSERT IGNORE INTO users (username, email, full_name, display_name, password_hash,
                          role, office_id, require_password_change)
SELECT LOWER(o.office_acronym),
       CONCAT(LOWER(o.office_acronym), '@ams-portal.local'),
       o.office_name,
       o.office_acronym,
       '$2b$12$eYn/08ASuRZfWepdU/3W6uosoKKhPSk/jZjmIYtHzmPEZfqnbrB3W',
       'Office', o.office_id, 1
  FROM offices o
 WHERE o.can_requisition = 1
   AND o.office_acronym <> 'AMS';

DROP TEMPORARY TABLE IF EXISTS seed_items;

INSERT IGNORE INTO schema_migrations (version) VALUES ('002_sample_data');
