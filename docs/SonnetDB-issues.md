# SonnetDB 所需数据库能力

验证日期：2026-10-08 至 2026-10-09。SqlSugar PR [#1457](https://github.com/DotNetNext/SqlSugar/pull/1457) 以正式 `SonnetDB 4.0.0` 为依据。SqlSugar 的能力不应因选择 SonnetDB 被提前禁用；缺口在数据库侧实现，适配层只负责原生方言与现有接口。

## 当前待补合同

复用其他 ORM 已提出的共同需求，避免给相同数据库能力重复立项。新建 issue 均限定 SqlSugar 的实际公开接口，不要求复制 PostgreSQL 全部方言。

| SonnetDB issue | 所需合同 | SqlSugar 入口/证据 |
| --- | --- | --- |
| [#227](https://github.com/IoTSharp/SonnetDB/issues/227) | 关系 DDL 事务提交与回滚 | 基类 CodeFirst.InitTables 在事务内建表，4.0.0 实测失败 |
| [#245](https://github.com/IoTSharp/SonnetDB/issues/245) | 原生临时关系表与 CTAS | Fastest 的工作表生命周期、BackupTable；4.0.0 SQL 实测失败 |
| [#246](https://github.com/IoTSharp/SonnetDB/issues/246) | 批量关联更新的派生来源、事务内连续写入 | UPDATE FROM VALUES/派生查询失败；同一事务第二次关联更新失败 |
| [#229](https://github.com/IoTSharp/SonnetDB/issues/229) | 多 SELECT 与 NextResult | Ado.GetDataSetAll/多类型 SqlQuery，4.0.0 实测失败 |
| [#247](https://github.com/IoTSharp/SonnetDB/issues/247) | 标准存储过程命令与输出参数 | Ado.UseStoredProcedure/SugarParameter.Direction；CALL 可用，CommandType.StoredProcedure 仍拒绝 |
| [#233](https://github.com/IoTSharp/SonnetDB/issues/233) | 普通事务锁定读 | Queryable/Storageable.TranLock(Wait)，4.0.0 实测失败 |
| [#248](https://github.com/IoTSharp/SonnetDB/issues/248) | NOWAIT 锁定读 | TranLock(DbLockType.Error)；只跟踪现有立即失败选项，不增加锁模式 |
| [#249](https://github.com/IoTSharp/SonnetDB/issues/249) | 按行动态 JSON path 和对象数组谓词 | JsonField/JsonIndex/JsonListObjectAny；参数化静态 path 已支持，无需重复 |
| [#223](https://github.com/IoTSharp/SonnetDB/issues/223) | 数据库内字符串聚合 | Subqueryable.SelectStringJoin；STRING_AGG/GROUP_CONCAT 实测失败 |
| [#239](https://github.com/IoTSharp/SonnetDB/issues/239) / [#237](https://github.com/IoTSharp/SonnetDB/issues/237) | 持久化表列备注与反向元数据 | CodeFirst 描述、DbMaintenance remarks、DbFirst |
| [#250](https://github.com/IoTSharp/SonnetDB/issues/250) | 约束目录、索引选项、自增起点 | CodeFirst.ChangeKey、SugarIndex.Desc/包含列、SetAutoIncrementInitialValue |
| [#226](https://github.com/IoTSharp/SonnetDB/issues/226) | 有数据表的主键结构演进 | CodeFirst 存量主键列及定义迁移 |
| [#235](https://github.com/IoTSharp/SonnetDB/issues/235) | decimal 参数有效位保真 | 高有效位 ADO 参数往返实测丢失精度 |
| [#236](https://github.com/IoTSharp/SonnetDB/issues/236) | DATETIME 参数亚毫秒精度 | ADO 日期参数往返实测丢失 ticks |
| [#241](https://github.com/IoTSharp/SonnetDB/issues/241) | ADD/ALTER DECIMAL(p,s) 精度与元数据 | DbMaintenance.AddColumn 的精度声明及回读 |

新建的 #245–#250 含复现/源码依据、接口影响和验收。其他条目复用共享需求；不把关闭为 duplicate 或“仅定义不支持边界”理解为能力已实现。

## 原 #171–#183 的更新

旧清单基于 3.1.0，原十三条 SQL issue 已关闭，4.0.0 已交付其中多项能力：CTE、基础窗口、CAST、普通字符串/日期函数、聚合 DISTINCT、更多连接与集合运算、BETWEEN/ILIKE、基础 JSON、DECIMAL、TIME 和按位运算。

提供程序已移除旧清单产生的大面积拒绝，使用原生映射与通用生成流程。分页、窗口、字符串/日期、JSON、复合键和批量 DML 已有消费者实际回归；不能用旧矩阵继续永久限制这些入口，也不能从 issue 状态推导未验证的完整兼容性。

#190 的关闭只定义了乐观并发及拒绝悲观锁的产品边界；实际锁定读仍按 #233/#248 追踪。#191 的关系表关联更新也不等于派生来源和事务连续写入已完成，剩余范围见 #246。

## 验证和交付

基础功能与一万行集合写入回归见 [SonnetDB-compatibility.md](SonnetDB-compatibility.md)。`--check-capabilities` 会对尚缺的核心合同执行成功断言，失败返回非零；它不是期待 NotSupportedException 的拒绝矩阵。

每项交付须包含数据库实现、实际回归、正式 NuGet/Server 版本和 SqlSugar 消费者验证。当前不声称已经完整满足 SqlSugar，也不要求为 SonnetDB 改造其核心框架。
