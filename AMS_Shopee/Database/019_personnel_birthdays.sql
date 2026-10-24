-- 019_personnel_birthdays.sql
-- This is for the birthday check on My accountability in e-SRS. The person's birthday is asked
-- before their documents are shown. The person sets it once (or AMS sets it in e-PINS > Employees),
-- and I only save it as a one-way hash, so nobody can read it back from the database.
-- After 5 wrong tries the person is locked out for 15 minutes. AMS can unlock or reset it in e-PINS.
--
-- Run once in phpMyAdmin (database db64557) before publishing e-SRS and e-PINS.

CREATE TABLE IF NOT EXISTS personnel_birthdays (
    personnel_id   INT UNSIGNED NOT NULL,
    birthday_hash  VARCHAR(200) NOT NULL,
    set_by         VARCHAR(100) NOT NULL DEFAULT 'self',
    set_at         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    failed_tries   INT          NOT NULL DEFAULT 0,
    locked_until   DATETIME     NULL COMMENT 'UTC',
    last_verified  DATETIME     NULL,
    PRIMARY KEY (personnel_id),
    CONSTRAINT fk_bday_personnel FOREIGN KEY (personnel_id) REFERENCES ro_personnel (personnel_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO schema_migrations (version) VALUES ('019_personnel_birthdays');
