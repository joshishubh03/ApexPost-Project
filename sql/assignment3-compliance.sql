/*
    Evidence queries for Part A: lists every PK, FK (with delete behaviour), UNIQUE, CHECK and DEFAULT
    constraint, the rowversion columns and the audit columns in ApexPostDb. Run in SSMS and compare with DESIGN.md.
*/
USE ApexPostDb;
SET NOCOUNT ON;

SELECT t.name AS table_name, kc.name AS constraint_name, kc.type_desc
FROM sys.key_constraints AS kc
INNER JOIN sys.tables AS t ON t.object_id = kc.parent_object_id
ORDER BY t.name, kc.type_desc;

SELECT OBJECT_NAME(fk.parent_object_id) AS child_table, fk.name AS fk_name,
       OBJECT_NAME(fk.referenced_object_id) AS parent_table, fk.delete_referential_action_desc AS on_delete
FROM sys.foreign_keys AS fk
ORDER BY child_table, fk_name;

SELECT OBJECT_NAME(i.object_id) AS table_name, i.name AS index_name, i.has_filter, i.filter_definition
FROM sys.indexes AS i
WHERE i.is_unique = 1 AND i.is_primary_key = 0 AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
ORDER BY table_name;

SELECT OBJECT_NAME(cc.parent_object_id) AS table_name, cc.name, cc.definition
FROM sys.check_constraints AS cc
ORDER BY table_name, cc.name;

SELECT OBJECT_NAME(dc.parent_object_id) AS table_name, c.name AS column_name, dc.definition
FROM sys.default_constraints AS dc
INNER JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
ORDER BY table_name, column_name;

SELECT OBJECT_NAME(c.object_id) AS table_name, c.name AS column_name, TYPE_NAME(c.system_type_id) AS type_name
FROM sys.columns AS c
WHERE c.name IN ('RowVersion', 'CreatedAtUtc', 'CreatedBy', 'ModifiedAtUtc', 'ModifiedBy')
  AND OBJECTPROPERTY(c.object_id, 'IsUserTable') = 1
ORDER BY table_name, c.column_id;

-- Masked identity storage: the only identity column is varchar(16) and every stored value is masked.
SELECT c.name, TYPE_NAME(c.system_type_id) AS type_name, c.max_length
FROM sys.columns AS c WHERE c.object_id = OBJECT_ID('dbo.IdentityVerifications') AND c.name LIKE '%Document%';
SELECT COUNT(*) AS unmasked_rows FROM dbo.IdentityVerifications WHERE MaskedDocumentIdentifier NOT LIKE '****%';
