-- -------------------------------------------------------------------
-- ams_stockwatch, 017: PROPERTY ON A PAR / ICS FOLLOWS ITS RETURN
-- Property register items can be linked to the PAR / ICS they're on (property_items.document_id).
-- When the PAR / ICS is returned, its items change to "Returned". I keep the status they had
-- before in this column so "Mark as not returned" can put it back.
--   property_items.status_before_return  VARCHAR(40) NULL
-- Run after 016. I wrote it without information_schema so it runs on MonsterASP.
-- If the first line says "#1060 Duplicate column name", the column is already there,
-- so just run the rest.
-- -------------------------------------------------------------------

ALTER TABLE property_items
  ADD COLUMN status_before_return VARCHAR(40) NULL AFTER status;

-- Items that are already on a PAR / ICS that was returned before this update
UPDATE property_items i
  JOIN property_documents d ON d.document_id = i.document_id
   SET i.status_before_return = i.status, i.status = 'Returned'
 WHERE d.returned_on IS NOT NULL AND i.status <> 'Returned';

INSERT IGNORE INTO schema_migrations (version) VALUES ('017_property_items_returns');
