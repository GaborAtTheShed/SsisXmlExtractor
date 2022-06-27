# SsisXmlExtractor

To match the SSIS objects to actual SQL objects, you'll need to populate a `sql_objects.txt` file and put it in the execution directory (along with the SSIS dtsx packages you want to get analysed).

```
SET NOCOUNT ON;

SELECT all_tables.name
		 , all_tables.type_desc
		 , all_tables.db
FROM
(
SELECT name, type_desc, 'db1' AS db FROM db1.sys.objects
WHERE type IN	('U', 'V', 'P')

UNION ALL

SELECT name, type_desc, 'db2' AS db FROM db2.sys.objects
WHERE type IN	('U', 'V', 'P')

UNION ALL

...
```