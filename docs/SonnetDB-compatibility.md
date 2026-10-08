# SonnetDB 与 SqlSugar 适配

验证日期：2026-10-08 至 2026-10-09。依赖正式 NuGet `SonnetDB 4.0.0`，目标框架 `net10.0`。验证使用 Windows/.NET 10 的嵌入式连接；远程服务、发布裁剪与其他部署方式尚未执行本次回归。

## 适配原则

SonnetDB 是 SqlSugar 的一个数据库提供程序，按已有接口实现连接、参数、SQL 方言和元数据。通用查询、表达式解析、实体映射、CodeFirst 及 Fastest 的流程复用 SqlSugar；不在 ORM 中重做数据库解析、能力白名单、迁移锁或逐行执行回退。

数据库已有的能力应正常接入，缺少的原生能力在 SonnetDB 跟踪。不能通过禁用 SqlSugar 公开 API、忽略调用选项、改变数据语义或修改调用方事务来声称兼容完成。SonnetDB 可以提供更多能力，适配的目标范围是 SqlSugar 已有公共接口。

## SqlSugar 公共代码改动

相对 PR 的上游基线，保留六个文件的必要接入或通用兼容（新增 27 行、删除 14 行）：

- `DbType` 增加 SonnetDB。
- 工厂按现有外置提供程序方式加载 `SqlSugar.SonnetDBCore`。
- 动态 FastBuilder 接收已有 `DbFastestProperties` 的 `IsOffIdentity`、`BatchSize`、`IsIgnoreInsertError`。这是所有动态提供程序都可使用的属性传递修复，无新接口或 SonnetDB 特例。
- 差异日志复用 PostgreSQL 已有的主键类型转换，并推广到所有数据库，避免把整数主键错误转为字符串参数。
- 通用读回补齐 TimeOnly → TimeSpan，复用已有转换辅助方法。DbFirst 默认值转换改为受保护 virtual 钩子，具体的原生类型与默认值翻译留在 SonnetDB 驱动。两者都可用于其他驱动，没有新增公共接口。

撤销公共 Queryable/Storageable 的 SonnetDB 专属拒绝、Fastest 的专属日期开关和其他非必需框架改造。驱动只保留必要的双引号标识符、LIMIT/OFFSET、RETURNING、原生函数和 DDL/元数据方言。

## Feature10：真正的批量插入与更新

| 入口 | 执行方式 | 一万行验证 |
| --- | --- | --- |
| `Insertable(list).PageSize(1000)` | 每批一条参数化多行 `INSERT ... VALUES (...),(...)` | 十条 INSERT，影响行数与实际数据一致 |
| `Updateable(list).PageSize(1000)` | 每批一条参数化 `UPDATE ... SET ... CASE WHEN` | 十条 UPDATE，精确匹配主键；另测复合键和增量更新 |
| `Fastest.BulkCopy` | 参数化多行 INSERT，默认每批 1000 行 | 十条 INSERT，实际一万行 |
| `Fastest.BulkUpdate` | 批量写入唯一工作表，再单条 `UPDATE ... FROM` | 十条导入 INSERT、一条目标 UPDATE，实际一万行 |
| `Fastest.BulkMerge` | 工作表一次集合 UPDATE，再 `INSERT ... SELECT ... WHERE NOT EXISTS` | 覆盖同时插入与更新；没有逐行查询或执行 |

批次中的行列循环只用于构造 SQL 和参数，每批通过一次数据库命令执行。保留 SqlSugar 自带的 PageSize/BatchSize 入口，不用逐条执行代替批量能力。

当前 Fastest 使用普通关系工作表，名字唯一，正常流程和执行失败路径会删除工作表；工作表拥有独立自增主键，允许部分列输入和多条尚未分配主键的实体。实际验证包含部分列 DataTable 更新、自增记录合并、OffIdentity、BatchSize 和重复键忽略。

SonnetDB 4.0.0 尚无原生临时表/CTAS，且禁止事务内 DDL。因此当前 Fastest 工作表路径不能在调用方已有事务内创建；BulkMerge 的两条目标写入也不具备无事务时的单句 UPSERT 原子性。对应 [#245](https://github.com/IoTSharp/SonnetDB/issues/245)、[#227](https://github.com/IoTSharp/SonnetDB/issues/227) 必须完成。派生来源及事务内连续关联更新追踪 [#246](https://github.com/IoTSharp/SonnetDB/issues/246)。不能拆出用户事务或恢复逐行回退来掩盖这些限制。

## 已执行验证与剩余缺口

功能回归覆盖维护建表/元数据、自增键同步异步回写、一万行插入与更新、Fastest 合并/边界、分页、集合查询、窗口、字符串位置、JSON 存在/缺失与数组成员、日期回读、复合键、关联更新和集合 DML 事务回滚。另实际编译 DbFirst 生成模型，在 `de-DE` 文化下检查原生类型默认值，并验证可空 TIME 实体读回及差异日志的 SQL、参数和插入行回查。

普通 decimal 小数往返已通过；高有效位参数仍有数据库精度缺陷。不能由小数用例通过宣称所有 decimal 保真。DATETIME 亚毫秒精度和 ALTER DECIMAL 精度声明也仍需修复。

完整性检查包含 SqlSugar 基类 CodeFirst、多个结果集、原生临时表、TranLock，以及上述三个精度合同。当前这些检查失败，完整检查返回非零；这表示数据库能力尚未完整交付，不是期望拒绝测试的“通过”。具体问题及共同 issue 见 [SonnetDB-issues.md](SonnetDB-issues.md)。

运行：

```powershell
dotnet build Src/Asp.NetCore2/SqlSugar.SonnetDBCore/SqlSugar.SonnetDBCore.csproj
dotnet run --project Src/Asp.NetCore2/SonnetDBTest/SonnetDBTest.csproj
dotnet run --project Src/Asp.NetCore2/SonnetDBTest/SonnetDBTest.csproj -- --check-capabilities
```

## 合并条件

本 PR 当前是适配整改与数据库合同验证，不能宣称 SqlSugar 全能力已满足。数据库缺口必须实现、通过回归并包含在正式发布版本中；更新依赖后，重新执行消费者端到端验证。关闭 issue、仅说明不支持或只在源码 main 实现，都不等于正式包已经可用。

保留关系 DML、查询和已通过的批量路径；完整 CodeFirst/事务批量/ADO 等待数据库原生合同补齐。所需公共接口的验证完成后再申请合并。
