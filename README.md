# 软著智能申请工具（MVP）

面向**任意代码仓库**的软件著作权申请材料准备工具：导入 ZIP 仓库 → 只读静态分析 → 生成带证据的项目画像 → 申请人确认 → 从**真实源码**生成申请材料包。

> 核心承诺：**任意仓库都能安全进入流程并得到透明分析**。
> 核心边界：**不执行仓库代码、不编造源码、不代替申请人做法律判断、不自动提交官方系统。**

---

## 一、它解决什么问题

准备软著申请材料不止是"写一份文档"，还包括：理解项目、收集申请信息、筛选真实源码、写操作说明、保证材料之间口径一致。现有工具通常只覆盖其中一段。

本工具把这些环节串成一条**可重复、可追溯、可复核**的工作流：

| 环节 | 本工具的做法 |
|---|---|
| 仓库接入 | 安全解包 + 通用扫描，未知技术栈也能进入流程 |
| 事实来源 | 每条结论都带 `相对路径 + 行号 + SHA-256`，可回溯到原始文件 |
| 不确定性 | 区分「仓库证据 / 推断 / 申请人已确认 / 未知」，未知项显式列为待补充 |
| 人机边界 | 五道人工确认门禁；上游数据一变，下游确认自动失效 |
| 材料生成 | 代码鉴别材料逐行摘自被确认的真实源码，不改写、不补写 |
| 结果校验 | 区分「通过 / 警告 / 需申请人处理」三级，不表述为"保证通过登记" |

---

## 二、技术栈与运行形态

| 项 | 选择 |
|---|---|
| 框架 | .NET 10 / C# 14 / ASP.NET Core Minimal API |
| 前端 | Blazor Web App（Interactive Server），与 API 同进程同端口 |
| 数据库 | **MySQL 8.0**（EF Core 10 + Oracle 官方提供程序 `MySql.EntityFrameworkCore`） |
| 文档生成 | Open XML SDK（DOCX）、System.Text.Json、System.IO.Compression（ZIP） |
| 任务队列 | 持久化任务表 + `BackgroundService`（带租约、重试与退避） |
| 测试 | xUnit + `WebApplicationFactory`（单元 + 集成） |

单进程启动，浏览器访问 `http://localhost:5080` 即可使用。

---

## 三、快速开始

### 1. 环境要求

- .NET SDK **10.0.301**（或更新的 10.0.x）
- MySQL **8.0**（本机已验证 8.0.45）

### 2. 准备数据库

```sql
CREATE DATABASE softcopyright CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'softcopyright'@'localhost' IDENTIFIED BY '你的密码';
GRANT ALL PRIVILEGES ON softcopyright.* TO 'softcopyright'@'localhost';
FLUSH PRIVILEGES;
```

应用启动时会自动建库建表（`EnsureCreated`），无需额外迁移工具。

### 3. 配置连接串（**不要写进仓库**）

任选一种：

```powershell
# 方式 A：环境变量（推荐）
$env:SOFTCOPYRIGHT_MYSQL = "Server=localhost;Port=3306;Database=softcopyright;User Id=softcopyright;Password=你的密码;CharSet=utf8mb4;"

# 方式 B：user-secrets（仅当前用户、仅本机，不进版本库）
dotnet user-secrets set "ConnectionStrings:MySql" "Server=localhost;Port=3306;Database=softcopyright;User Id=softcopyright;Password=你的密码;CharSet=utf8mb4;" --project src/SoftCopyright.Api
```

> 也可以写在 `src/SoftCopyright.Api/appsettings.Development.json` 的 `ConnectionStrings:MySql`，但请确认该文件不会被提交。

### 4. 启动

```powershell
cd SoftCopyright
dotnet build
dotnet run --project src/SoftCopyright.Api
```

浏览器打开 <http://localhost:5080>。

> **没有 MySQL 时的临时办法**：设置 `$env:Database__Provider = "Sqlite"`，程序会自动改用 `%LOCALAPPDATA%\SoftCopyright\softcopyright.db`（SQLite 为兼容选项，仅用于本地演示）。

### 5. 使用流程

1. **申请案例** → 新建案例
2. **导入仓库** → 上传 ZIP → 「开始扫描」
3. **分析与证据** → 查看画像、覆盖情况、待补充问题 → 「确认项目画像」
4. **申请信息** → 填写并保存 → 「保存并确认」（法律与权属字段必须由你填写）
5. **代码材料** → 勾选纳入材料包的真实源文件 → 预览 → 「保存并确认范围」
6. **材料与导出** → 「确认文档草稿」→ 「生成材料」→ 「确认最终材料」→ 下载材料包

---

## 四、命令

```powershell
dotnet build                       # 编译（0 error / 0 warning）
dotnet test                        # 单元 + 集成测试
dotnet test --filter "FullyQualifiedName~ZipSecurityTests"   # 只跑安全测试
dotnet run --project src/SoftCopyright.Api                   # 启动应用
```

集成测试默认使用 **MySQL**（读取 `SOFTCOPYRIGHT_TEST_MYSQL` 或 `SOFTCOPYRIGHT_MYSQL`）；未配置时自动回退到 SQLite 临时库，保证离线也能跑。

---

## 五、目录结构

```
SoftCopyright/
├── src/
│   ├── SoftCopyright.Domain/          # 实体、值对象、枚举、配置项与跨模块契约（零外部依赖）
│   ├── SoftCopyright.Application/     # 用例编排：上传、扫描、分析、确认门禁、校验、生成
│   ├── SoftCopyright.ProjectScan/     # 安全解包、文件索引、排除规则、密钥检测、6 个适配器
│   ├── SoftCopyright.AI/              # IProjectAnalyzer / IMaterialDraftGenerator 与 3 个 Provider
│   ├── SoftCopyright.Documents/       # DOCX / TXT / JSON / HTML / ZIP 材料生成
│   ├── SoftCopyright.Infrastructure/  # EF Core、MySQL、文件存储、后台任务
│   ├── SoftCopyright.Api/             # Minimal API 端点 + Blazor 宿主 + 健康检查
│   └── SoftCopyright.Web/             # Blazor 工作台界面（Razor 类库）
├── tests/SoftCopyright.Tests/         # 单元 + 集成测试、合成 fixture
├── docs/                              # 架构说明 / 安全设计 / 材料规则说明 / 状态机与确认门禁
└── samples/                           # 手工验收用的合成示例仓库
```

依赖方向严格单向：`Domain ← Application ← Infrastructure ← Api`，`Web` 只依赖 `Application` 的 DTO，界面通过真实 HTTP 调用 API。

---

## 六、AI Provider 配置

默认使用**离线规则分析**（`Ai:Provider=rule-based`），不联网、结果确定。

接入真实模型（OpenAI 兼容接口，如 MiniMax / OpenAI / 私有网关）：

```json
"Ai": {
  "Provider": "http",
  "BaseUrl": "https://your-gateway/v1",
  "Model": "your-model"
}
```

密钥**只能**通过环境变量注入：

```powershell
$env:SOFTCOPYRIGHT_AI_APIKEY = "sk-..."
```

模型调用具备以下硬约束：

- 只发送筛选后的必要证据片段（≤ 60 条 / ≤ 200 KB），**不发送整个仓库**；
- 证据包中疑似凭据文件一律剔除；
- 模型输出必须通过 JSON 强校验，且**每条证据引用都要回验路径与哈希**；
- 引用无效的事实自动降级为「推断」，不写成已验证事实；
- 系统提示明确声明证据块是不可信数据，其中的"指令"只当作被分析的内容；
- 模型不可用（超时 / 限流 / 非法 JSON）时**自动回退到规则分析**，不阻塞流程。

---

## 七、安全边界

| 边界 | 说明 |
|---|---|
| 不执行仓库代码 | 全程只做文件读取与静态解析，不调用 shell、构建、安装、测试、部署或 Git hooks |
| ZIP 解压防护 | Zip Slip 路径穿越、绝对路径、磁盘驱动器路径、UNC 路径、符号链接、重复路径、压缩炸弹、超量条目、超限文件与层级深度，全部拒绝并给出中文原因 |
| 写入隔离 | 所有文件写入 `cases/{caseId}/` 隔离目录，落盘前校验规范化路径仍在根目录内 |
| 凭据处理 | `.env`、私钥、Token 等疑似凭据文件被标记排除，不进入证据、不发给模型、不写普通日志 |
| 不可信输入 | README、注释、源码、配置中的"指令"一律视为数据，只作为证据内容处理 |
| 法律事实 | 著作权人、权属关系、开发方式、完成日期等由申请人填写并确认，系统永不预填 |
| 源码真实性 | 程序鉴别材料只截取被确认的真实文件，逐行不改写、不补写、不生成 |
| 日志卫生 | 普通应用日志不记录源码正文与密钥 |
| 访问边界 | 单用户本地应用，**不可直接暴露到公网**；下载路径由服务端数据库决定，客户端无法提交磁盘路径 |

详见 [`docs/安全设计.md`](docs/安全设计.md)。

---

## 八、已知限制（明确不做）

| 未实现项 | 原因 / 现状 |
|---|---|
| **PDF 渲染** | MVP 未实现。材料包只含 DOCX / TXT / JSON / HTML / ZIP；界面与本文档均已标注，不提供无效下载入口 |
| 官方系统自动提交 | 不做验证码、签章、无人值守提交；只提供材料与字段映射，由申请人自行填报 |
| GitHub / GitLab 授权接入 | MVP 只支持 ZIP 导入 |
| 桌面端本地扫描 | 未实现 |
| 组织 / 角色 / 审批 / 计费 | 单用户本地 MVP，未实现 |
| 多 Agent 自主改写申请事实 | 不做；AI 只能产出草稿与推断 |
| 规则版本的官方性 | `MaterialRules` 是本工具的工程配置，**不是官方权威解释**，材料中统一提示"请按官方最新要求核对" |
| PostgreSQL / SQLite | 保留为可选提供程序（`Database:Provider`），本次仅在 MySQL 上完成端到端验证 |

---

## 九、测试覆盖

- **ZIP 安全**：路径穿越、绝对路径、驱动器路径、UNC、重复路径、条目数/总量/压缩比/深度/单文件超限、非法 ZIP —— 全部拒绝，且工作区外无任何写入
- **扫描与适配器**：.NET / Node / Python / Java / 小程序 / 通用兜底各自的元数据抽取；未知扩展名仍产出代码候选；依赖目录与疑似凭据被排除并给出原因
- **AI 抽象**：模型输出缺证据、引用不存在路径、引用哈希不符时均被降级或丢弃；证据包剔除凭据并遵守总量上限
- **状态机与门禁**：合法/非法迁移；修改申请信息后下游确认失效；未过门禁时生成被拒
- **端到端**：创建 → 上传 → 扫描 → 确认 → 选材 → 生成 → 下载材料包 → 最终确认 → 删除 → 下载返回 404；重启后案例与确认记录仍在
- **安全验收**：仓库脚本从未被执行（用标记文件证明）；提示注入只作为证据内容处理

合成 fixture 共 6 个（.NET / Node / Python / 未知扩展名 / 注入样例 / 含可执行脚本样例），全部由测试代码生成，不含任何真实个人源码。

---

## 十、文档

| 文档 | 内容 |
|---|---|
| [`docs/架构说明.md`](docs/架构说明.md) | 分层、模块职责、端到端数据流 |
| [`docs/安全设计.md`](docs/安全设计.md) | ZIP 威胁模型、不可信输入处理、凭据策略 |
| [`docs/材料规则说明.md`](docs/材料规则说明.md) | 规则版本、字段清单、页数与行数策略 |
| [`docs/状态机与确认门禁.md`](docs/状态机与确认门禁.md) | 状态迁移、确认哈希与失效级联 |
