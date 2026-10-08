# SonnetDB 验证

依赖正式 SonnetDB 4.0.0，使用 .NET 10。运行：

```powershell
dotnet run --project Src/Asp.NetCore2/SonnetDBTest/SonnetDBTest.csproj
```

在独立临时数据库执行功能回归并自动清理，包含一万行 Insertable/Updateable/Fastest 插入和集合更新。通过 OnExecutingChangeSql 统计实际命令，确认每批一条集合 SQL，覆盖自增键、部分列、批次选项、decimal 小数、TIME、窗口、日期、JSON、复合键及事务回滚。

完整性检查：

```powershell
dotnet run --project Src/Asp.NetCore2/SonnetDBTest/SonnetDBTest.csproj -- --check-capabilities
```

额外验证标准 CodeFirst 的事务 DDL、多结果集、原生临时表、TranLock、高有效位 decimal、DATETIME 亚毫秒和 ALTER DECIMAL 精度。SonnetDB 4.0.0 当前缺少这些合同，因此完整检查预期返回非零，不能据基础功能通过宣称完整兼容。

元数据建表通过已有 DbMaintenance 接口准备功能用例；基类 CodeFirst 单独接受完整性检查。QueryChecks 保留窗口、字符串位置、日期时间回读、JSON 存在/缺失、关联更新及差异日志的语义回归。MetadataChecks 实际编译 DbFirst 生成模型，在 `de-DE` 文化下检查默认值，并验证可空 TimeOnly/TimeSpan 实体读回。

详见 [兼容说明](../../../docs/SonnetDB-compatibility.md) 和 [数据库 issue](../../../docs/SonnetDB-issues.md)。本次测试为嵌入式模式，不代表远程或其他发布方式已验证。
