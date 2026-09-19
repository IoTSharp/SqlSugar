# SonnetDB 能力缺口 Issue

本文记录 SqlSugar 适配 SonnetDB 时确认的通用数据库能力缺口。内容只提出 SonnetDB 的 SQL 需求，不把 SqlSugar 内部实现细节或上层业务路线图带入 SonnetDB。

所有 13 个条目已于 2026-09-20 提交到 [IoTSharp/SonnetDB](https://github.com/IoTSharp/SonnetDB)。下方保留完整的最小复现、目标语义和验收标准，方便后续讨论和回归测试。关联背景见 [SonnetDB-compatibility.md](SonnetDB-compatibility.md)。

## 已发布 Issue

| 编号 | GitHub Issue |
| --- | --- |
| SDB-SQL-01 | [#171：支持非递归 CTE](https://github.com/IoTSharp/SonnetDB/issues/171) |
| SDB-SQL-02 | [#172：支持关系表 ANSI 窗口函数](https://github.com/IoTSharp/SonnetDB/issues/172) |
| SDB-SQL-03 | [#173：支持标准 CAST](https://github.com/IoTSharp/SonnetDB/issues/173) |
| SDB-SQL-04 | [#174：补齐常用字符串函数](https://github.com/IoTSharp/SonnetDB/issues/174) |
| SDB-SQL-05 | [#175：提供 date_diff 和日期格式化](https://github.com/IoTSharp/SonnetDB/issues/175) |
| SDB-SQL-06 | [#176：支持聚合 DISTINCT](https://github.com/IoTSharp/SonnetDB/issues/176) |
| SDB-SQL-07 | [#177：扩展标准连接类型](https://github.com/IoTSharp/SonnetDB/issues/177) |
| SDB-SQL-08 | [#178：扩展集合运算](https://github.com/IoTSharp/SonnetDB/issues/178) |
| SDB-SQL-09 | [#179：补齐 BETWEEN 和 ILIKE](https://github.com/IoTSharp/SonnetDB/issues/179) |
| SDB-SQL-10 | [#180：扩展 JSON 查询能力](https://github.com/IoTSharp/SonnetDB/issues/180) |
| SDB-SQL-11 | [#181：支持精确 DECIMAL/NUMERIC](https://github.com/IoTSharp/SonnetDB/issues/181) |
| SDB-SQL-12 | [#182：支持 TIME 类型](https://github.com/IoTSharp/SonnetDB/issues/182) |
| SDB-SQL-13 | [#183：支持关系 SQL 整数按位与/或运算符](https://github.com/IoTSharp/SonnetDB/issues/183) |

## 提交优先级

| 编号 | 建议优先级 | 原因 |
| --- | --- | --- |
| SDB-SQL-01 | 高 | CTE 是通用组合查询能力，能减少 SqlSugar 复杂派生查询的嵌套。 |
| SDB-SQL-02 | 高 | `OVER(...)` 是 ORM 常用的分页、排名和分组统计能力。 |
| SDB-SQL-03 | 高 | 显式类型转换是 ORM 表达式翻译的基础。 |
| SDB-SQL-04 | 高 | 常见字符串 API 是业务筛选和投影的高频需求。 |
| SDB-SQL-05 | 中 | 日期差值/格式化的边界语义必须先定义，不能靠不等价改写。 |
| SDB-SQL-06 | 中 | 聚合 DISTINCT 常用于报表和去重统计。 |
| SDB-SQL-07 | 中 | 右/全/交叉连接提升查询覆盖，但可在部分场景由应用改写。 |
| SDB-SQL-08 | 中 | 集合运算扩展改善复杂查询表达力。 |
| SDB-SQL-09 | 中 | `BETWEEN` 和大小写无关匹配是常用谓词，但需先确定 Unicode/索引语义。 |
| SDB-SQL-10 | 中 | JSON 数组和包含谓词是 SqlSugar JSON API 的主要缺口。 |
| SDB-SQL-11 | 高 | 精确十进制不能用 FLOAT 替代，否则金额和聚合会失真。 |
| SDB-SQL-12 | 中 | `TIME` 是 ORM 映射 TimeOnly/TimeSpan 的基础类型。 |
| SDB-SQL-13 | 中 | 整数按位运算是筛选、标志位和权限表达式的常见基础能力。 |

## SDB-SQL-01：支持非递归 CTE

**标题**：`[SQL] 支持非递归 WITH ... AS (...) 公共表表达式`

### 问题

SonnetDB 词法分析器认识 `WITH`，但 `SqlParser.ParseStatement` 没有把它作为查询起始语句处理；当前 `WITH` 用于部分 DDL 选项。关系查询只能使用嵌套派生表。

### 最小复现

```sql
WITH active_devices AS (
    SELECT id, tenant_id
    FROM devices
    WHERE enabled = TRUE
)
SELECT tenant_id, COUNT(*) AS device_count
FROM active_devices
GROUP BY tenant_id;
```

### 期望

至少支持非递归 CTE：多个按顺序引用的 CTE、列名列表、CTE 在 FROM/IN/EXISTS 中使用，以及 CTE 之后的 `ORDER BY`/分页。递归 CTE 可以作为后续独立范围。

### 验收标准

- 解析器为 SELECT 前的 `WITH` 建立 AST；不与 DDL `WITH` 混淆。
- 执行器确保同一 CTE 的物化/内联策略不改变 SQL 结果和参数绑定。
- 覆盖单 CTE、多 CTE、CTE JOIN、CTE 聚合、CTE 参数、无效循环引用和列数不匹配。
- 通过关系表与文档/时序来源的边界测试，明确不支持的组合。

### 对 SqlSugar 的影响

SqlSugar 可安全承载复杂 `SqlQueryable`、派生查询和用户原始 SQL，不必把所有组合都展开为深层子查询。

## SDB-SQL-02：支持关系表 ANSI 窗口函数 OVER

**标题**：`[SQL] 支持关系表 ANSI 窗口函数 OVER(...)`

### 问题

SonnetDB 已有时序原生窗口函数，但当前 SQL 词法分析器/解析器没有 `OVER`，关系查询没有 ANSI 窗口帧。SqlSugar 的 `RowNumber`、`RowCount`、`RowSum`、`RowAvg`、`RowMin`、`RowMax` 和 `PartitionBy` 会依赖这种语义。

### 最小复现

```sql
SELECT id,
       tenant_id,
       ROW_NUMBER() OVER (PARTITION BY tenant_id ORDER BY created_at, id) AS row_no,
       COUNT(*) OVER (PARTITION BY tenant_id) AS tenant_count
FROM devices;
```

### 期望

先覆盖关系表的 `ROW_NUMBER`、`COUNT`、`SUM`、`AVG`、`MIN`、`MAX` 和 `OVER (PARTITION BY ... ORDER BY ...)`。窗口帧（`ROWS/RANGE`）可分阶段实现，但当前支持范围必须明确。

### 验收标准

- 解析器支持 `OVER`、`PARTITION BY`、窗口内 `ORDER BY`。
- 对无 `ORDER BY` 的窗口函数定义稳定、文档化的行为；`ROW_NUMBER` 必须要求确定顺序或明确其非确定性。
- NULL、重复排序键、多个分区、空输入、与普通聚合混用的规则都有测试。
- 明确关系窗口与现有时序原生窗口函数的语义和性能边界。

### 对 SqlSugar 的影响

可解除对 RowNumber/RowCount 系列 API 和 `PartitionBy` 的提供程序级禁用。

## SDB-SQL-03：支持标准 CAST 和明确的类型转换规则

**标题**：`[SQL] 支持 CAST(expr AS type) 和明确的类型转换规则`

### 问题

当前 SonnetDB 的日期函数可以处理 DATETIME 与 Unix 毫秒，但没有已确认的通用 `CAST(expr AS type)` 语法。SqlSugar 的 `.ToString()`、`.ToInt32()`、`.ToInt64()`、`.ToDouble()`、`.ToDecimal()`、`.ToBool()`、`.ToDate()`、`.ToGuid()` 不能安全地从 PostgreSQL 或 SQL Server cast 语法迁移。

### 最小复现

```sql
SELECT CAST(id AS STRING) AS id_text,
       CAST(enabled AS INT) AS enabled_number,
       CAST(created_at AS DATETIME) AS created_at_value
FROM devices;
```

### 期望

提供标准 `CAST`，或提供具有同等完整文档的转换函数。首批类型至少覆盖 `INT`、`FLOAT`、`BOOL`、`STRING`、`DATETIME` 和 JSON 标量；不应承诺 SonnetDB 没有原生语义的 UUID/任意精度 decimal。

### 验收标准

- 明确 NULL 传播、溢出、格式错误、布尔字符串、时间文本和 Unix 毫秒的行为。
- 明确数值到字符串的区域性格式，建议固定为不随区域设置变化的规则。
- 不允许隐式转换改变筛选或索引结果；需要时应定义显式转换的索引使用策略。
- 覆盖 SELECT、WHERE、GROUP BY、HAVING、UPDATE SET 和参数绑定。

### 对 SqlSugar 的影响

允许实现常见 CLR 转换，而不是提供程序为每个转换保留数据库专属、不兼容的字符串拼接或转换语法。

## SDB-SQL-04：补齐关系表常用字符串函数

**标题**：`[SQL] 补齐关系表常用字符串函数`

### 问题

当前内置标量函数已确认有 `concat`、`lower`、`upper`、`regexp_like`，但没有 SqlSugar 常用的 `trim`、`replace`、`substring`、`length` 和位置查找函数。直接继承 PostgreSQL 的 `strpos` 或 SQL Server 的 `LEN`/`CHARINDEX` 会产生无效 SQL。

### 最小复现

```sql
SELECT trim(name) AS normalized_name,
       replace(code, '-', '') AS compact_code,
       substring(name, 1, 3) AS prefix,
       length(name) AS name_length,
       position('ab' IN name) AS position_1_based
FROM devices;
```

### 期望

支持标准或明确文档化的 `trim`、`replace`、`substring`、`length`、`position`。如果另选函数名，也需要给出跨数据库可迁移的语义。

### 验收标准

- 所有函数的 NULL 传播、Unicode 字符计数、空字符串、越界、起始下标和长度语义有测试。
- `substring` 明确采用 SQL 的一基索引或其他规则；不要让客户端猜测。
- `trim` 支持默认空白和可选字符集的范围应明确。
- 关系表、文档和时序查询各自支持范围写入文档。

### 对 SqlSugar 的影响

解除 `Trim`、`Replace`、`Substring`、`Length`、`CharIndex` 的翻译禁用；若位置函数为一基索引，提供程序可为 .NET `IndexOf` 明确做减一转换。

## SDB-SQL-05：提供明确的 date_diff 和日期格式化能力

**标题**：`[SQL] 提供明确的 date_diff 和日期格式化能力`

### 问题

SonnetDB 已支持 `date_part`、`date_add`、Unix 时间转换，但没有已确认的 `date_diff` 和日期格式化函数。用秒数相减或多个 `date_part` 相减不能保真 `DateDiff` 的跨月、跨年、时区和边界计数语义。

### 最小复现

```sql
SELECT date_diff('day', started_at, finished_at) AS day_diff,
       date_diff('month', started_at, finished_at) AS month_diff,
       format_datetime(started_at, 'yyyy-MM-dd') AS started_date
FROM jobs;
```

### 期望

新增 `date_diff(part, start, end)` 和可控的日期格式化函数，或明确拒绝格式化而仅提供标准化 ISO 输出。`part` 的支持范围应与 `date_part`/`date_add` 对齐并文档化。

### 验收标准

- 规定 day/month/year 差是历法边界数、完整间隔数还是持续时间截断值。
- 覆盖 UTC、本地时间、DateTimeOffset、Unix 毫秒、DST 边界、NULL 和负差值。
- 格式化必须使用固定可审计格式令牌，不受服务器区域设置隐式影响。

### 对 SqlSugar 的影响

可评估支持 `DateDiff`、`DateIsSameDay`、`DateIsSameByType`、`WeekOfYear` 和日期 `.ToString(format)`；在语义未落定前提供程序应继续拒绝。

## SDB-SQL-06：支持关系查询的聚合 DISTINCT

**标题**：`[SQL] 支持关系查询的聚合 DISTINCT`

### 问题

`SELECT DISTINCT` 已支持，但 `COUNT(DISTINCT column)`、`SUM(DISTINCT column)`、`AVG(DISTINCT column)` 不是当前关系聚合的已确认语法。SonnetDB 的某些时序扩展聚合不等价于标准关系聚合语义。

### 最小复现

```sql
SELECT COUNT(DISTINCT tenant_id) AS tenant_count,
       SUM(DISTINCT amount) AS unique_amount_total,
       AVG(DISTINCT amount) AS unique_amount_average
FROM orders;
```

### 期望

支持标准聚合参数 `DISTINCT`，第一阶段至少完成 `COUNT(DISTINCT expr)`；是否支持 `SUM`/`AVG` 可按类型和资源预算分阶段发布。

### 验收标准

- NULL 的聚合 DISTINCT 规则与标准 SQL 一致并有测试。
- 覆盖分组、HAVING、空表、重复数值、字符串和大整数精度。
- 说明内存预算、溢写或拒绝策略，避免高基数输入无界占用。

### 对 SqlSugar 的影响

解除 `AggregateDistinctCount`、`AggregateDistinctSum`、`AggregateDistinctAvg` 的提供程序禁用。

## SDB-SQL-07：扩展标准连接类型

**标题**：`[SQL] 扩展标准连接类型`

### 问题

当前解析器只接受普通/`INNER JOIN` 和 `LEFT [OUTER] JOIN`，关系执行器的 `JoinKind` 也只有 Inner/Left。SqlSugar 的 RIGHT、FULL、CROSS 连接不能生成可执行 SonnetDB SQL。

### 最小复现

```sql
SELECT l.id, r.id
FROM left_items AS l
FULL OUTER JOIN right_items AS r ON l.id = r.id;
```

```sql
SELECT l.id, r.id
FROM left_items AS l
RIGHT JOIN right_items AS r ON l.id = r.id;
```

### 期望

至少补齐 `RIGHT JOIN` 和 `FULL OUTER JOIN`，并明确是否支持 `CROSS JOIN`。如果只支持关系表，应在解析器/执行器和文档中限制数据模型范围。

### 验收标准

- 覆盖匹配、左侧缺失、右侧缺失、NULL 键、重复键、空输入和多连接链。
- FULL JOIN 的列冲突、类型和排序规则明确。
- 连接优化器不能因 JoinKind 扩展改变 LEFT JOIN 已有语义。

### 对 SqlSugar 的影响

解除对应 JoinType 的提供程序禁用；在此之前必须明确报错，而不能把 RIGHT/FULL 错译为 INNER/LEFT。

## SDB-SQL-08：扩展集合运算

**标题**：`[SQL] 扩展 UNION ALL、INTERSECT 和 EXCEPT 集合运算`

### 问题

当前 `UNION` 具有去重语义；`UNION ALL`、`INTERSECT`、`EXCEPT` 没有解析器/执行器路径。将 `UNION ALL` 改成 `UNION` 会丢失重复行，属于数据语义错误。

### 最小复现

```sql
SELECT tenant_id FROM direct_roles
UNION ALL
SELECT tenant_id FROM inherited_roles;
```

```sql
SELECT tenant_id FROM direct_roles
INTERSECT
SELECT tenant_id FROM enabled_tenants;
```

### 期望

支持标准集合运算及统一的复合查询尾部 `ORDER BY`/分页；需要定义列数、类型兼容、NULL 去重和结果列名规则。

### 验收标准

- `UNION ALL` 保留重复行和输入分支顺序的可观察语义。
- `INTERSECT`、`EXCEPT` 的 NULL、重复行和多列行比较有测试。
- 无效列数/类型组合给出可诊断错误。
- 对大输入明确内存预算和溢写/拒绝策略。

### 对 SqlSugar 的影响

可支持 SqlSugar 的 UnionAll 和用户原始 SQL 的常用集合查询，而不是把它们静默降级为去重 UNION。

## SDB-SQL-09：补齐常用谓词 BETWEEN 和 ILIKE

**标题**：`[SQL] 补齐常用谓词 BETWEEN 和 ILIKE`

### 问题

当前确认的比较面为普通比较、`LIKE`、`NOT LIKE`、正则和 `IN`，没有 `BETWEEN` 或 `ILIKE`。提供程序不应直接把 `ILIKE` 改为 `lower(left) LIKE lower(right)`，因为 Unicode 大小写、转义和索引行为尚未定义。

### 最小复现

```sql
SELECT id, name
FROM devices
WHERE created_at BETWEEN @begin AND @end
  AND name ILIKE @pattern;
```

### 期望

支持 `BETWEEN`/`NOT BETWEEN` 与 `ILIKE`/`NOT ILIKE`，或在文档中给出与其完全等价且索引友好的官方替代语法。

### 验收标准

- `BETWEEN` 的闭区间、NULL 和不同数值/日期类型行为有测试。
- `ILIKE` 的 Unicode case-folding、ESCAPE、通配符和区域设置策略明确。
- 明确普通索引、前缀索引或全文索引对 ILIKE 的可用性。

### 对 SqlSugar 的影响

可支持 `SqlFunc.Between` 和不区分大小写的文本筛选；在此之前提供程序应拒绝而不是生成未知关键字。

## SDB-SQL-10：扩展 JSON 标量与数组查询能力

**标题**：`[SQL] 扩展 JSON 标量与数组查询能力`

### 问题

当前关系表已确认 `json_value(json, '$.path')`，且 path 必须为字符串字面量。SqlSugar 的 JSON API 还会涉及数组长度、数组成员、对象字段存在性、对象/数组包含和多层动态 path；PostgreSQL 的 JSONB 运算符不能作为替代。

### 最小复现

```sql
SELECT json_array_length(metadata, '$.tags') AS tag_count
FROM devices
WHERE json_exists(metadata, '$.tags[0]')
  AND json_contains(metadata, '$.tags', 'industrial');
```

### 期望

在保留 `json_value` 的基础上，设计一套数据库中立、参数可绑定、可索引的 JSON API。建议优先评估：`json_exists`、`json_array_length`、数组成员谓词和对象/数组包含谓词。

### 验收标准

- JSON path 的静态/参数化边界、缺失路径、JSON null、SQL NULL、对象、数组和标量差异明确。
- 所有函数在错误 JSON、非法 path、超深嵌套和大数组时有可控错误与资源限制。
- 能与 JSON path 索引协作时，EXPLAIN 或文档应说明可用条件。
- 不以 PostgreSQL `::jsonb`、`->`、`->>`、`@>` 作为唯一公开语法。

### 对 SqlSugar 的影响

可逐步支持 `JsonArrayLength`、`JsonContainsFieldName`、`JsonArrayAny`、`JsonListObjectAny` 等 API；在能力与语义确认前提供程序应继续拒绝这些调用。

## SDB-SQL-11：支持关系表精确 DECIMAL/NUMERIC

**标题**：`[SQL] 支持关系表精确 DECIMAL/NUMERIC 类型`

### 问题

当前关系表只有 `FLOAT`，它不能保留十进制金额、计量值和聚合结果的精确语义。SqlSugar 的 `decimal`、`Currency` 与 `VarNumeric` 若被隐式转换为 `double`，会在写入、比较和 `SUM` 时产生不可接受的精度损失。

### 最小复现

```sql
CREATE TABLE ledger (
    id INT PRIMARY KEY,
    amount DECIMAL(18, 4) NOT NULL
);

INSERT INTO ledger (id, amount) VALUES (1, 0.1000), (2, 0.2000);
SELECT amount, SUM(amount) AS total FROM ledger;
```

### 期望

支持具有明确精度和小数位的 `DECIMAL(p, s)` 或 `NUMERIC(p, s)`，至少覆盖常见的 `DECIMAL(18,4)` 与 `DECIMAL(28,8)`。如果采用固定小数或缩放整数实现，也应公开等价的 DDL、参数与聚合语义。

### 验收标准

- 明确精度、范围、舍入、溢出、NULL、比较和排序规则。
- 参数绑定、字面量、INSERT、UPDATE、GROUP BY、SUM、AVG 与索引比较均不经由二进制浮点降级。
- 覆盖 `0.1 + 0.2`、负数、最大精度、不同小数位、聚合和事务回滚。
- 驱动元数据能返回精度和小数位，便于 ORM CodeFirst/DbFirst 映射。

### 对 SqlSugar 的影响

解除 `decimal`、`Currency`、`VarNumeric` 的提供程序级禁用，使金额和精确计量模型不必改为字符串或手写缩放整数。

## SDB-SQL-12：支持关系表 TIME 类型

**标题**：`[SQL] 支持关系表 TIME 类型及明确的仅时间值语义`

### 问题

关系表当前没有独立 `TIME` 类型。将 .NET `TimeOnly` 或 `TimeSpan` 伪装成 `DATETIME` 会引入无意义日期、时区和比较语义，提供程序只能明确拒绝该映射。

### 最小复现

```sql
CREATE TABLE shifts (
    id INT PRIMARY KEY,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL
);

SELECT id FROM shifts WHERE start_time < TIME '08:30:00';
```

### 期望

提供关系表 `TIME` 类型，或提供等价且完整文档化的仅时间值类型与文字量语法。应明确精度、范围、是否允许时区、与 `DATETIME` 的显式转换规则。

### 验收标准

- 覆盖参数绑定、DDL、元数据、INSERT/UPDATE、比较、排序、索引和 NULL。
- 明确午夜、跨日业务规则、秒以下精度及 `24:00:00` 是否可用。
- 不允许隐式附加服务器日期或时区而改变比较结果。
- ADO.NET 驱动能够以 `TimeOnly` 或清晰定义的 `TimeSpan` 形式读写。

### 对 SqlSugar 的影响

解除 `TimeOnly`/`TimeSpan` 的提供程序级禁用；在此之前，日期值可使用 `DateOnly` 映射为当日零点 `DATETIME`，仅时间值必须继续拒绝。

## SDB-SQL-13：支持关系 SQL 整数按位与/或运算符

**标题**：`[SQL] 支持关系 SQL 整数按位与/或运算符 &、|`

### 问题

SonnetDB 关系 SQL 当前无法解析整数按位与和按位或。词法器不接受 `&`、`|`，关系表达式 AST 也没有对应的二元运算节点。SqlSugar 等 ORM 需要在投影、筛选和更新表达式中使用这些运算；当前只能在应用层计算。

### 最小复现

```sql
SELECT 6 & 3 AS and_value;
SELECT 6 | 3 AS or_value;
```

当前分别在运算符位置报告无法识别字符 `&` 或 `|`。

### 期望结果

```text
and_value = 2
or_value = 7
```

同时支持整数列在 `SELECT`、`WHERE`、`UPDATE` 中参与按位运算；括号优先级应明确。NULL、非整数和溢出行为请提供稳定的中文错误说明或文档约定。

### 验收标准

- 词法器识别 `&`、`|`，并与逻辑 AND/OR 关键字区分。
- 解析器建立正确优先级和括号 AST。
- 执行器支持 Int64 常量、列值、筛选、更新及混合算术表达式。
- 覆盖 NULL、溢出、非整数输入的稳定诊断。
- 增加中文回归测试和 SQL 文档。

### 对 SqlSugar 的影响

在能力实现前，SonnetDB 提供程序会在翻译阶段拒绝 C# `&`/`|` 和 `SqlFunc.BitwiseAnd`/`BitwiseInclusiveOR`，避免把错误 SQL 发送到驱动层。

## 暂不建议直接立项的项目

下列项目当前不应以“语法兼容”名义直接提交实现 issue，因为首先需要 SonnetDB 的事务/并发产品语义决定：

- `FOR UPDATE`、`NOWAIT`、`SKIP LOCKED` 与 SqlSugar `TranLock`。
- SQL Server 风格 `WITH(NOLOCK)` 等表提示。
- SQL Server/PostgreSQL 多表 UPDATE/DELETE 的锁定、可见性和受影响行语义。
- UUID、随机数、厂商特有全文检索函数等没有 SonnetDB 原生类型或统一语义的功能。

在没有明确一致性、隔离级别和错误语义前，SqlSugar 提供程序应当抛出清晰异常，不应静默忽略调用或伪造 SQL。
