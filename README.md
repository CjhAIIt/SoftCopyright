# 知识产权申请材料工具（MVP）

面向**任意代码仓库**的知识产权申请材料准备工具：导入 ZIP 或 Git 仓库 → 只读静态分析 → 生成带证据的项目画像 → 申请人确认 → 从**真实源码**生成申请材料包。

工具内有**两条互相独立的申请线**，进入首页后先选择要走哪一条：

| | 软著材料书写 | 专利材料书写 |
|---|---|---|
| 面向 | 软件著作权登记 | 发明 / 实用新型专利申请 |
| 事实来源 | 项目画像 + 申请信息 | 项目画像 + **技术交底书**（唯一真源） |
| 前置门禁 | 画像 → 申请信息 → 代码范围 → 草稿 | 画像 → 交底书与权利要求就绪 → 草稿 |
| 代码范围 | 必须勾选，代码鉴别材料逐行摘自真实源码 | 不需要，专利申请文件不含代码鉴别材料 |
| 产出 | 申请表信息、操作手册、代码鉴别材料、证据清单、校验报告 | 权利要求书、说明书、摘要、技术交底书 |
| 人工判断 | 著作权人、权属关系、开发方式等法律事实 | 保护范围、新颖性、创造性、充分公开（须由专利代理人复核） |

> 核心承诺：**任意仓库都能安全进入流程并得到透明分析**。
> 核心边界：**不执行仓库代码、不编造源码、不代替申请人做法律判断、不自动提交官方系统、不代为检索现有技术。**

---

## 一、它解决什么问题

准备申请材料不止是"写一份文档"，还包括：理解项目、收集申请信息、筛选真实源码、写操作说明、保证材料之间口径一致。现有工具通常只覆盖其中一段。

本工具把这些环节串成一条**可重复、可追溯、可复核**的工作流：

| 环节 | 本工具的做法 |
|---|---|
| 仓库接入 | 安全解包 + 通用扫描，未知技术栈也能进入流程 |
| 事实来源 | 每条结论都带 `相对路径 + 行号 + SHA-256`，可回溯到原始文件 |
| 不确定性 | 区分「仓库证据 / 推断 / 申请人已确认 / 未知」，未知项显式列为待补充 |
| 人机边界 | 五道人工确认门禁；上游数据一变，下游确认自动失效 |
| 材料生成 | 代码鉴别材料逐行摘自被确认的真实源码，不改写、不补写 |
| 专利撰写 | 技术交底书是唯一真源；权利要求结构由规则引擎确定，模型只可润色措辞 |
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

应用启动时会自动建库并**应用 EF Core 迁移**（`MigrateAsync`），迁移文件位于 `src/SoftCopyright.Infrastructure/Data/Migrations/`，已随仓库提交。

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

### 5. 首次使用：注册账号与引导

第一次打开会跳到**注册页**：

1. **注册** —— 填登录名（3–40 字符）、显示名称、密码（≥8 位）。账号只存在本机数据库，密码只保存 PBKDF2 派生值。
2. **登录** —— 注册成功后自动进入引导页；以后直接登录即可。
3. **引导** —— 两步确认，决定"谁来帮你写"：

| 选择 | 结果 |
|---|---|
| **配置 API 密钥，使用模型辅助** | 填模型接口地址、模型名与 API 密钥。密钥用本机密钥文件做 AES-256-GCM 加密后存进本地数据库，不进仓库、不进日志。模型只可润色措辞，不得新增技术特征；输出必须通过 JSON 强校验与证据回验，失败自动回退到离线规则实现。 |
| **不配置，用免费的离线功能** | 二次确认后进入完全离线模式：扫描、取证、权利要求生成、说明书组装与校验全部由本机规则引擎完成，**不产生任何外部网络请求**。之后仍可在账号设置里接入模型。 |

材料按账号隔离：每个账号只看得到自己的案例。升级前遗留的无归属案例会被**第一个注册的账号**接管。

账号设置页在顶栏的用户名上（点击进入 `/account`），可以随时接入模型或改回离线模式。

#### 两种账号

| 类型 | 获取方式 | 权限 |
|---|---|---|
| **普通账号** | 在注册页自助注册 | 只能看到并管理自己的案例；可配置自己的模型接入 |
| **管理员账号** | 系统内置，登录名固定为 `admin` | 可查看**全部账号**及其接入状态（离线 / 已配置密钥）、删除账号、查看整体数据规模 |

内置管理员的默认密码为 `Cjh041217@`，**仓库中只保存它的 PBKDF2 哈希，不保存明文**。首次启动时若账号不存在则自动创建；已存在时只补齐管理员标记，**不会重置密码**。

部署时可用环境变量改掉默认密码（仅首次创建生效）：

```powershell
$env:SOFTCOPYRIGHT_ADMIN_PASSWORD = "换成你自己的强密码"
```

管理员账号不能删除自己，系统也会保证至少保留一个管理员。

### 6. 使用流程

首页是**入口选择页**，先点「软著材料书写」或「专利材料书写」进入对应工作区；两个工作区的案例列表互不相通。

**软著材料书写**（`/softcopyright`）

1. **软著材料书写** → 「新建软著案例」
2. **导入仓库** → 两种方式二选一：
   - **从 Git 仓库导入**：粘贴 `https://github.com/owner/repo`（或 `git@github.com:owner/repo.git`），可选分支/提交号 → 「克隆并扫描」，系统自动排队扫描
   - **从 ZIP 压缩包导入**：上传 ZIP → 「开始扫描」
3. **分析与证据** → 查看画像、覆盖情况、待补充问题 → 「确认项目画像」
4. **申请信息** → 填写并保存 → 「保存并确认」（法律与权属字段必须由你填写；开发方式、权利取得方式、发表状态等栏位是**受约束的官方选项**，只能从下拉里选）
5. **代码材料** → 勾选纳入材料包的真实源文件 → 预览 → 「保存并确认范围」
6. **材料与导出** → 「确认文档草稿」→ 「生成材料」→ 「确认最终材料」→ 下载材料包

**专利材料书写**（`/patent`）

1. **专利材料书写** → 「新建专利案例」
2. **导入仓库** → 与软著线相同，专利草稿的技术特征也依赖仓库证据
3. **分析与证据** → 「确认项目画像」
4. **专利交底书** → 填写技术领域、背景技术、技术问题、技术方案（用「；」分隔技术特征）、有益效果、附图说明，以及现有技术与其来源 → 「保存」→「生成专利草稿」
5. 查看生成的权利要求书、技术特征与证据对应表、实施例与专利校验结果 → 修正后「确认草稿」
6. **材料与导出** → 「生成材料」→ 下载专利申请材料包（权利要求书 / 说明书 / 摘要 / 技术交底书）

> 两条线的页面互不串门：专利案例打开「申请信息」「代码材料」页会提示改去交底书页，软著案例打开「专利交底书」页同样会被拦下。

---

## 四、部署到服务器

应用是标准 ASP.NET Core 进程，可以直接发布到服务器：

```powershell
dotnet publish src/SoftCopyright.Api -c Release -o ./publish
```

部署时需要落实的几件事：

| 项 | 做法 |
|---|---|
| **反向代理** | 放在 Nginx / IIS / 网关之后。应用已启用 `X-Forwarded-For / Proto / Host` 支持，登录 Cookie 的 Secure 标记与重定向地址会按原始协议计算 |
| **HTTPS** | 在代理层强制 HTTPS。Cookie 的 `Secure` 策略为 `SameAsRequest`，代理未透传协议时会退化为不加密 |
| **数据库** | MySQL 8.0。连接串用环境变量 `SOFTCOPYRIGHT_MYSQL` 或 user-secrets 注入，**不要写进仓库** |
| **数据目录** | `Storage:DataRoot` 指定工作目录（案例文件、产物、密钥文件都在这里）。密钥文件 `secret.key` **必须与数据库一起备份**，否则已存的 API 密钥无法解密 |
| **密钥文件** | `secret.key` 首次启动自动生成（32 字节随机，权限只对运行账号可读）。多实例部署需共享同一个文件，否则各实例互相无法解密 |
| **管理员密码** | 首次启动前设置 `SOFTCOPYRIGHT_ADMIN_PASSWORD`；不设置则使用内置默认密码 `Cjh041217@`，**公网部署务必改掉** |
| **实例名与状态说明** | `App:InstanceName` / `App:StatusNote` 可按部署环境改写顶栏文字 |
| **后台任务** | 任务队列持久化在数据库里，进程重启后未完成的任务会被重新领取。若部署多实例，同一数据库上的任务会被任一实例执行 |
| **反向代理超时** | 导入大型仓库时克隆可能耗时数分钟，代理的读超时要放宽到 10 分钟以上（对应 `GitImport:TimeoutSeconds`） |

推荐的最小部署检查清单：

```powershell
# 1. 配置连接串与管理员密码
$env:SOFTCOPYRIGHT_MYSQL = "Server=127.0.0.1;Port=3306;Database=softcopyright;User Id=softcopyright;Password=...;CharSet=utf8mb4;"
$env:SOFTCOPYRIGHT_ADMIN_PASSWORD = "换成强密码"

# 2. 发布并启动
dotnet publish src/SoftCopyright.Api -c Release -o ./publish
cd publish
$env:ASPNETCORE_URLS = "http://127.0.0.1:5080"
dotnet SoftCopyright.Api.dll
```

> 应用不会自己向官方系统提交任何内容，材料生成后仍需申请人在官方系统自行填报与签章。

---

## 五、命令

```powershell
dotnet build                       # 编译（0 error / 0 warning）
dotnet test                        # 单元 + 集成测试
dotnet test --filter "FullyQualifiedName~ZipSecurityTests"   # 只跑安全测试
dotnet test --filter "FullyQualifiedName~PatentFlowTests"    # 只跑专利端到端
dotnet run --project src/SoftCopyright.Api                   # 启动应用
```

集成测试默认使用 **MySQL**：优先读 `SOFTCOPYRIGHT_TEST_MYSQL`，其次读 `SOFTCOPYRIGHT_MYSQL`；未配置时自动回退到 SQLite 临时库，保证离线也能跑。MySQL 模式下**每个测试宿主使用独立的临时库**（`softcopyright_test_<guid>`），避免并行迁移冲突：

```powershell
$env:SOFTCOPYRIGHT_TEST_MYSQL = "Server=localhost;Port=3306;Database=softcopyright_test;User Id=softcopyright;Password=你的密码;CharSet=utf8mb4;"
dotnet test
```

数据库结构由 EF Core 迁移维护，开发期新增迁移：

```powershell
dotnet tool restore                              # 还原仓库内的 dotnet-ef（dotnet-tools.json）
dotnet ef migrations add <名字> --project src/SoftCopyright.Infrastructure --startup-project src/SoftCopyright.Api --output-dir Data/Migrations
```

---

## 六、目录结构

```
SoftCopyright/
├── src/
│   ├── SoftCopyright.Domain/          # 实体、值对象、枚举、配置项与跨模块契约（零外部依赖）
│   ├── SoftCopyright.Application/     # 用例编排：上传、扫描、分析、确认门禁、校验、生成、账号
│   ├── SoftCopyright.ProjectScan/     # 安全解包、文件索引、排除规则、密钥检测、6 个适配器
│   ├── SoftCopyright.AI/              # IProjectAnalyzer / IMaterialDraftGenerator 与 3 个 Provider，以及专利草稿生成器（规则版 / HTTP 版 / 路由）
│   ├── SoftCopyright.Documents/       # DOCX / TXT / JSON / HTML / ZIP 材料生成（软著与专利两套生成器）
│   ├── SoftCopyright.Infrastructure/  # EF Core、MySQL、文件存储、后台任务、密码派生与密钥保护
│   ├── SoftCopyright.Api/             # Minimal API 端点 + Cookie 认证 + Blazor 宿主 + 健康检查
│   └── SoftCopyright.Web/             # Blazor 工作台界面（Razor 类库）：入口页、登录注册、引导、两条工作区
├── tests/SoftCopyright.Tests/         # 单元 + 集成测试、合成 fixture
├── docs/                              # 架构说明 / 安全设计 / 材料规则说明 / 状态机与确认门禁
└── samples/                           # 手工验收用的合成示例仓库
```

依赖方向严格单向：`Domain ← Application ← Infrastructure ← Api`，`Web` 只依赖 `Application` 的 DTO，界面通过真实 HTTP 调用 API。

### 两条流程在代码里如何分开

`Application` 层按材料类型把差异收敛到**成对的实现**里，上层编排代码不再到处写 `if (materialKind == ...)`：

| 关注点 | 契约 | 软著实现 | 专利实现 |
|---|---|---|---|
| 流程描述（阶段文案、页面标签、产物清单、材料包命名） | `IMaterialFlowDescriptor` | `SoftCopyrightMaterialFlow` | `PatentMaterialFlow` |
| 校验规则集 | `IMaterialValidationRuleSet` | `SoftCopyrightValidationRuleSet` | `PatentValidationRuleSet` |
| 产物生成策略 | `IMaterialGenerationStrategy` | `SoftCopyrightGenerationStrategy` | `PatentGenerationStrategy` |
| 确认门禁 | `IConfirmationGate` | `SoftCopyrightConfirmationGate` | `PatentConfirmationGate` |

三张注册表（`MaterialFlowRegistry` / `MaterialValidationRuleSets` / `MaterialGenerationStrategies` / `ConfirmationGateRegistry`）按 `MaterialKind` 取对应实现。前端同样不硬编码差异：通过 `GET /api/flows` 拿到流程描述后渲染入口页、标签栏与阶段条。

关键 API：

| 端点 | 说明 |
|---|---|
| `GET /api/flows` | 两条流程的描述（阶段文案、页面标签、产物清单、工作区路径） |
| `GET /api/flows/{kind}` | 单条流程描述 |
| `GET /api/softcopyright/applicant-options` | 申请信息的选项目录（候选项、条件必填、跨字段约束），前端据此渲染下拉 |
| `POST /api/softcopyright/cases` | 新建软著案例（类型由端点决定） |
| `POST /api/patent/cases` | 新建专利案例（类型由端点决定） |
| `GET /api/cases/by-kind/{kind}` | 某条流程的案例列表（两个工作区互不相通） |

### 申请信息里的选项型字段

官方申请表里有 8 个栏位本来就是**选项**而不是自由填写：软件来源、开发方式、发表状态、权利取得方式、
权利范围、著作权人类别、申请办理方式、鉴别材料交存方式。它们集中定义在
`src/SoftCopyright.Domain/ApplicantOptionCatalog.cs`，前端通过 `GET /api/softcopyright/applicant-options`
拿候选项渲染下拉，**不在前端硬编码任何候选项**。

官方写明的联动要求也表达成数据：条件必填（如「已发表」→ 必须填首次发表日期与地点）、
跨字段约束（如「原始取得」→ 开发方式必须是「单独开发」）。保存时**值域非法或分类号格式非法直接被拒绝**，
未选项则允许先存草稿、在校验报告（`R-FIELDS-02`）里列为需处理项。

数据库存的是稳定的**规范选项码**，显示文本由选项目录解析；历史写法（「独立开发」「受让」等）
会被归一化，不会因为升级而失效。完整字段规范见 [`docs/软著申请字段规范.md`](docs/软著申请字段规范.md)。

---

## 七、Git 仓库导入配置

除 ZIP 外，还可直接粘贴仓库地址导入（GitHub / Gitee / GitLab）。导入后自动排队扫描。

```json
"GitImport": {
  "AllowedHosts": [ "github.com", "gitee.com", "gitlab.com" ],
  "AllowAnyHost": false,
  "AllowLocalFileUrls": false,
  "TimeoutSeconds": 600,
  "Shallow": true,
  "MaxWorkingTreeSizeMb": 512,
  "AccessTokenEnvironmentVariable": "SOFTCOPYRIGHT_GIT_TOKEN",
  "GitExecutablePath": ""
}
```

| 配置 | 说明 |
|---|---|
| `AllowedHosts` | 允许克隆的域名白名单；自建 GitLab 需加入此列表，或置 `AllowAnyHost=true` |
| `AllowLocalFileUrls` | 是否允许 `file://` 与本地路径（默认关闭） |
| `TimeoutSeconds` | 克隆超时（秒），超时后终止进程并清理目录 |
| `Shallow` | 浅克隆（只取最新一次提交），大幅降低流量与耗时 |
| `MaxWorkingTreeSizeMb` | 克隆后工作区大小上限，超过即放弃导入 |
| `AccessTokenEnvironmentVariable` | 私有仓库令牌的环境变量名，默认 `SOFTCOPYRIGHT_GIT_TOKEN` |

私有仓库通过令牌注入（令牌只来自环境变量，不写入仓库）：

```powershell
$env:SOFTCOPYRIGHT_GIT_TOKEN = "ghp_xxx"
```

> Git 导入只调用 `git` 自身：禁用仓库钩子、符号链接与 LFS 过滤器，禁止 `ext::` 等可执行类传输协议，克隆完成后删除 `.git`。**不会执行仓库中的任何脚本**。

---

## 八、AI Provider 配置

模型接入有**两个来源**，账号配置优先：

| 优先级 | 来源 | 适用场景 |
|---|---|---|
| 1 | **引导流程中为账号配置的设置** | 交互式使用；密钥加密后存本地库，按账号隔离 |
| 2 | 全局环境变量配置 | 无人值守场景（如后台任务没有账号上下文时的回退） |

默认使用**离线规则分析**（`Ai:Provider=rule-based`），不联网、结果确定；引导流程里选了"不配置"就是这个状态。

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

专利草稿生成器同样受这组约束，且额外要求：

- 模型**只润色措辞**，不得新增技术特征：返回的权利要求条数、编号与技术特征数必须与输入一致，否则整份输出作废并回退规则版；
- 摘要长度、权利要求编号与引用关系由本地校验器复核，不依赖模型自查；
- 默认仍为 `rule-based`，即**完全不联网**也能产出可用的申请文件初稿。

---

## 九、专利申请文件撰写

专利线的设计借鉴了开源社区（patentorney-mcp、claude-patent-creator、cn-patent-drafter、repo2patent 等）的共识做法，调研记录见 [`docs/专利撰写能力调研.md`](docs/专利撰写能力调研.md)。核心约束如下：

| 约束 | 实现方式 |
|---|---|
| **技术交底书是唯一真源** | 摘要、权利要求书、说明书全部由交底书派生，不存在第二份事实来源 |
| **权利要求结构由规则确定** | 1 条独立权利要求 + 逐条从属权利要求；引用关系、编号连续性由校验器强制检查 |
| **模型只润色，不得新增技术特征** | HTTP 草稿生成器校验摘要长度、编号与特征数不变；不通过即回退规则版 |
| **技术特征必须锚定证据** | 每项特征在证据包中检索出处（中文按二元组匹配）；找不到出处的标记为「待确认」，不写入权利要求正文 |
| **说明书五部分齐全** | 技术领域、背景技术、发明内容、附图说明、具体实施方式，逐项校验 |
| **至少 3 个实施例** | 不足时给出「可能公开不充分」警告 |
| **摘要 ≤ 300 字** | 超限即校验失败 |
| **不代为检索现有技术** | 现有技术由申请人填写且必须注明来源；缺失来源即判为「需申请人处理」 |
| **不编造对比文件** | 系统不生成任何对比文件编号 |

专利校验规则并入既有校验中心，编号以 `P-` 前缀区分：

| 规则 | 检查项 |
|---|---|
| `P-CLAIM-01/02/03` | 权利要求编号连续性、从属项引用关系、技术特征是否为占位内容 |
| `P-SPEC-01/02` | 说明书五部分完整性、实施例数量 |
| `P-ABS-01` | 摘要存在性与 300 字上限 |
| `P-EVID-01` | 技术特征与代码证据的绑定情况 |
| `P-PRIOR-01` | 现有技术及其来源 |
| `P-REVIEW-01` | 人工复核声明（保护范围、新颖性、创造性必须由专利代理人复核） |

> **本工具产出的是专利申请文件初稿。** 权利要求的保护范围、新颖性与创造性，以及说明书是否充分公开，**必须由专利代理人复核**。工具不代为判断，也不代为检索现有技术。界面与生成的文档中均有同样声明。

---

## 十、安全边界

| 边界 | 说明 |
|---|---|
| 不执行仓库代码 | 全程只做文件读取与静态解析，不调用 shell、构建、安装、测试、部署或 Git hooks |
| Git 克隆安全 | 克隆时把 `core.hooksPath` 指向空目录、禁用 LFS 过滤器与符号链接、禁止 `ext::` 等可执行类协议；克隆完成立即删除 `.git`，全过程不执行仓库脚本 |
| Git 地址白名单 | 仅允许 `GitImport:AllowedHosts` 中的域名；拒绝 `file://`（默认）、`ext::`、`--upload-pack` 等参数注入形式 |
| ZIP 解压防护 | Zip Slip 路径穿越、绝对路径、磁盘驱动器路径、UNC 路径、符号链接、重复路径、压缩炸弹、超量条目、超限文件与层级深度，全部拒绝并给出中文原因 |
| 写入隔离 | 所有文件写入 `cases/{caseId}/` 隔离目录，落盘前校验规范化路径仍在根目录内 |
| 凭据处理 | `.env`、私钥、Token 等疑似凭据文件被标记排除，不进入证据、不发给模型、不写普通日志 |
| **账号与凭据** | 密码用 PBKDF2-HMAC-SHA256（21 万次迭代）派生后存储，不保存明文；模型 API 密钥用服务端密钥文件做 AES-256-GCM 加密后落库，密钥文件在数据目录、不在仓库；登录 Cookie 为 HttpOnly + SameSite=Lax |
| **账号隔离** | 案例归属账号；列表、详情、删除等接口都按当前登录账号过滤，越权访问返回 404；API 未登录返回 401 而非跳转 |
| **管理边界** | 管理端接口要求 `Admin` 角色；管理员不能删除自己，系统至少保留一个管理员；删除账号会连带标记其名下案例为已删除 |
| 不可信输入 | README、注释、源码、配置中的"指令"一律视为数据，只作为证据内容处理 |
| 法律事实 | 著作权人、权属关系、开发方式、完成日期等由申请人填写并确认，系统永不预填 |
| 源码真实性 | 程序鉴别材料只截取被确认的真实文件，逐行不改写、不补写、不生成 |
| 日志卫生 | 普通应用日志不记录源码正文与密钥 |
| 访问边界 | 单用户本地应用，**不可直接暴露到公网**；下载路径由服务端数据库决定，客户端无法提交磁盘路径 |

详见 [`docs/安全设计.md`](docs/安全设计.md)。

---

## 十一、已知限制（明确不做）

| 未实现项 | 原因 / 现状 |
|---|---|
| **PDF 渲染** | MVP 未实现。材料包只含 DOCX / TXT / JSON / HTML / ZIP；界面与本文档均已标注，不提供无效下载入口 |
| **现有技术检索** | 不接入 BigQuery / EPO / PatentsView 等外部专利库，也不编造对比文件；现有技术由申请人填写并注明来源 |
| **专利附图生成** | 不生成流程图与结构图；说明书保留「附图说明」章节，由申请人补充图号与附图 |
| **专利形式审查** | 只做结构性检查（章节、编号、引用关系、字数），不对新颖性、创造性、充分公开作判断 |
| 官方系统自动提交 | 不做验证码、签章、无人值守提交；只提供材料与字段映射，由申请人自行填报 |
| 私有仓库免密导入 | 公开仓库直接克隆；私有仓库需配置 SOFTCOPYRIGHT_GIT_TOKEN 环境变量 |
| 桌面端本地扫描 | 未实现 |
| 组织 / 角色 / 审批 / 计费 | 单用户本地 MVP，未实现 |
| 多 Agent 自主改写申请事实 | 不做；AI 只能产出草稿与推断 |
| 规则版本的官方性 | `MaterialRules` 是本工具的工程配置，**不是官方权威解释**，材料中统一提示"请按官方最新要求核对" |
| PostgreSQL / SQLite | 保留为可选提供程序（`Database:Provider`），本次仅在 MySQL 上完成端到端验证 |

---

## 十二、测试覆盖

- **ZIP 安全**：路径穿越、绝对路径、驱动器路径、UNC、重复路径、条目数/总量/压缩比/深度/单文件超限、非法 ZIP —— 全部拒绝，且工作区外无任何写入
- **Git 导入**：地址解析与域名白名单；`ext::`、`--upload-pack`、参数注入被拒；本地仓库克隆后 `.git` 被删除；导入后自动完成扫描并产出正确的画像与版本
- **扫描与适配器**：.NET / Node / Python / Java / 小程序 / 通用兜底各自的元数据抽取；未知扩展名仍产出代码候选；依赖目录与疑似凭据被排除并给出原因
- **AI 抽象**：模型输出缺证据、引用不存在路径、引用哈希不符时均被降级或丢弃；证据包剔除凭据并遵守总量上限
- **状态机与门禁**：合法/非法迁移；修改申请信息后下游确认失效；未过门禁时生成被拒
- **专利撰写**：技术特征逐条绑定证据；权利要求编号连续且从属项引用独立项；摘要不超过 300 字；实施例数量与去重；权利要求正文不自带编号；技术领域措辞不重复
- **端到端**：创建 → 上传 → 扫描 → 确认 → 选材 → 生成 → 下载材料包 → 最终确认 → 删除 → 下载返回 404；重启后案例与确认记录仍在
- **专利端到端**：创建专利案例 → 导入仓库并扫描 → 填写交底书 → 生成草稿 → 确认草稿 → 生成并下载专利材料包（权利要求书 / 说明书 / 摘要 / 技术交底书）
- **账号与引导**：注册后自动登录并跳转引导；密码错误无法登录；引导可选择完全离线的免费功能；配置密钥后启用模型且**数据库中不出现明文密钥**；案例只对所属账号可见，越权访问返回 404；内置管理员可用固定密码登录、能查看全部账号与接入状态、不能删除自己但能删除普通账号；普通账号访问管理端返回 403
- **配置绑定**：数组型配置项不会被默认值叠加成重复项（Git 域名白名单、排除目录、必填字段清单）
- **申请信息选项**：选项目录覆盖 8 个官方选项字段；候选项取值与标签不重复；历史写法能归一化、非法写法不能；条件必填与「原始取得须单独开发」的跨字段约束都能被检出；保存时非法取值与非法分类号被拒绝（400）
- **安全验收**：仓库脚本从未被执行（用标记文件证明）；提示注入只作为证据内容处理

合成 fixture 共 6 个（.NET / Node / Python / 未知扩展名 / 注入样例 / 含可执行脚本样例），全部由测试代码生成，不含任何真实个人源码。

---

## 十三、文档

| 文档 | 内容 |
|---|---|
| [`docs/架构说明.md`](docs/架构说明.md) | 分层、模块职责、端到端数据流 |
| [`docs/安全设计.md`](docs/安全设计.md) | ZIP 威胁模型、不可信输入处理、凭据策略 |
| [`docs/材料规则说明.md`](docs/材料规则说明.md) | 规则版本、字段清单、页数与行数策略 |
| [`docs/软著申请字段规范.md`](docs/软著申请字段规范.md) | 官方申请表里的选项型字段、条件必填与跨字段约束 |
| [`docs/状态机与确认门禁.md`](docs/状态机与确认门禁.md) | 状态迁移、确认哈希与失效级联 |
| [`docs/专利撰写能力调研.md`](docs/专利撰写能力调研.md) | 开源社区自动化专利撰写方案调研与本工具的借鉴取舍 |
