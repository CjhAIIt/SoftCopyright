# API 参考

Web 端（`SoftCopyright.Api`）对外的 HTTP 接口全集。原生客户端**不走这些接口**——它直接调用引擎层（见 [`客户端说明.md`](客户端说明.md)），这份文档只描述服务端形态。

- 基地址：默认 `http://localhost:5080`
- 认证：Cookie（`SoftCopyright.Auth`）；未登录访问受保护端点返回 **401**，权限不足返回 **403**
- 编码：请求与响应均为 `application/json; charset=utf-8`（上传除外）
- 枚举：JSON 里以字符串输出（`MaterialKind`、`CaseStage`、`ConfirmationStage` 等）

## 一、错误响应

所有异常统一映射为同一形状：

```json
{ "error": { "code": "validation", "message": "「开发方式」的取值「瞎写的」不在允许的候选项之内。" } }
```

| HTTP | `code` | 含义 |
|---|---|---|
| 400 | `validation` | 输入校验失败（字段非法、分类号格式错误、归档被拒等） |
| 401 | — | 未登录（`/api/*` 受保护端点） |
| 403 | — | 权限不足（非管理员访问 `/api/admin/*`） |
| 404 | `not_found` | 资源不存在，**或对当前账号不可见**（不区分两者，防止探测） |
| 409 | `conflict` | 状态冲突（阶段不允许、乐观并发失败、门禁未通过） |
| 500 | `internal_error` | 服务端异常（日志里有细节，响应里没有） |

---

## 二、账号与引导

### `POST /api/auth/register`

表单字段：`userName`、`displayName`、`password`。注册成功直接签发登录 Cookie。

**响应是 302 重定向**，不是 JSON：已通过引导 → `/`；未通过 → `/onboarding`；失败 → `/register?error=...`

### `POST /api/auth/login`

表单字段：`userName`、`password`。同样 302 重定向（`/` 或 `/onboarding`，失败跳 `/login?error=...`）。

### `POST /api/auth/logout`

清除登录 Cookie，302 跳 `/login`。

### `GET /api/account/me` 🔒

当前账号信息：

```json
{
  "id": "…", "userName": "admin", "displayName": "系统管理员",
  "onboardingCompleted": true, "aiAssistantEnabled": false,
  "hasAiConfiguration": false, "isAdmin": true,
  "createdAt": "2026-11-04T…", "lastLoginAt": "2026-11-04T…"
}
```

### `POST /api/account/ai` 🔒

表单字段：`baseUrl`、`model`、`apiKey`、`returnTo`。三项（Base URL / 模型 / 密钥）**都必须填**，
否则 302 回 `returnTo` 并带 `?error=...`；成功同样 302。密钥用 AES-256-GCM 加密后入库。

### `POST /api/account/ai/offline` 🔒

表单字段：`returnTo`。改为完全离线模式，不保存任何模型配置。同样是 302 重定向。

---

## 三、管理端 🔒 Admin

| 端点 | 说明 |
|---|---|
| `GET /api/admin/accounts` | 全部账号列表（含接入状态：离线（免费）/ 已配置密钥、案例数） |
| `DELETE /api/admin/accounts/{id}` | 删除账号。不能删自己；系统至少保留一个管理员 |
| `GET /api/admin/overview` | 数据总览：账号数、案例数（软著/专利）、产物数、待处理与失败任务数、疑似凭据文件数 |

---

## 四、流程与选项目录

| 端点 | 说明 |
|---|---|
| `GET /api/flows` | 两条材料流程的描述（阶段文案、页面标签、产物清单、工作区路径） |
| `GET /api/flows/{kind}` | 单条流程描述。`kind` ∈ `SoftwareCopyright` / `Patent` |
| `GET /api/softcopyright/applicant-options` | 申请信息的**选项目录**：8 个选项型字段的候选项、条件必填规则、跨字段约束 |

选项目录是客户端与 Web 端共用的领域事实（`ApplicantOptionCatalog`），前端据此渲染下拉，不硬编码候选项。详见 [`软著申请字段规范.md`](软著申请字段规范.md)。

---

## 五、案例

以下端点都挂在 `/api/cases` 下，**需要登录**，并统一挂 `CaseOwnershipFilter`：带案例 ID 的请求会先校验「该案例对当前账号可见」，不可见即 404。

| 端点 | 说明 |
|---|---|
| `POST /api/softcopyright/cases` | 新建软著案例。请求体 `{ "name": "案例名" }` |
| `POST /api/patent/cases` | 新建专利案例 |
| `GET /api/cases/` | 当前账号的全部案例 |
| `GET /api/cases/by-kind/{kind}` | 按材料类型列出（两个工作区互不相通） |
| `GET /api/cases/{id}` | 案例详情（阶段、状态、确认状态、产物数等） |
| `DELETE /api/cases/{id}` | 删除案例：写审计摘要 → 删库行 → 物理删目录。之后一律 404 |
| `POST /api/cases/{id}/upload` | 上传 ZIP。`multipart/form-data`，字段名 `file` |
| `POST /api/cases/{id}/import-git` | Git 导入。请求体 `{ "url": "…", "branch": null, "commit": null }` |
| `POST /api/cases/{id}/scan` | 触发扫描（排后台任务） |
| `GET /api/cases/{id}/jobs` | 该案例的后台任务（类型、状态、进度、失败原因） |
| `GET /api/cases/{id}/analysis` | 项目画像、覆盖情况、待确认问题、证据列表 |
| `GET /api/cases/{id}/applicant-info` | 申请信息（含字段来源标注与确认状态） |
| `PUT /api/cases/{id}/applicant-info` | 保存申请信息。选项字段的值必须落在选项目录内 |
| `GET /api/cases/{id}/code-candidates` | 代码候选文件（路径、行数、SHA-256、是否已勾选） |
| `GET /api/cases/{id}/code-candidates/preview` | 预览源文件。查询参数 `path`、`maxLines` |
| `PUT /api/cases/{id}/code-selection` | 保存代码范围。请求体 `{ "selectedPaths": ["src/A.cs"] }` |
| `GET /api/cases/{id}/confirmations` | 各阶段的确认状态（含是否已失效） |
| `POST /api/cases/{id}/confirm/{stage}` | 确认某个门禁。请求体 `{ "note": "确认说明" }` |
| `POST /api/cases/{id}/generate` | 生成材料（排后台任务） |
| `GET /api/cases/{id}/artifacts` | 产物索引（文件名、类型、大小、是否失效） |
| `GET /api/cases/{id}/validation` | 校验结果（三级结论） |
| `GET /api/cases/{id}/package/download` | 下载材料包 ZIP |
| `GET /api/cases/{id}/patent/disclosure` | 专利交底书（专利线） |
| `PUT /api/cases/{id}/patent/disclosure` | 保存专利交底书 |
| `POST /api/cases/{id}/patent/draft` | 生成专利草稿（排后台任务） |

`{stage}` ∈ `ProjectProfile` / `ApplicantInfo` / `CodeScope` / `DocumentDraft` / `FinalPackage`。

---

## 六、产物下载

| 端点 | 说明 |
|---|---|
| `GET /api/artifacts/{artifactId}/download` | 下载单个产物。**按产物回溯到案例后同样校验归属**，越权返回 404 |

---

## 七、健康检查

| 端点 | 说明 |
|---|---|
| `GET /health` | 存活探针，返回 200 |

---

## 八、界面路由（Blazor）

界面本身是 Blazor Web App 的页面路由，不属于 API，但会调用上面的接口：

| 路由 | 页面 |
|---|---|
| `/` | 入口选择页（软著 / 专利） |
| `/softcopyright` `/patent` | 两条流程各自的案例列表 |
| `/cases/{id}/import` | 导入仓库 |
| `/cases/{id}/analysis` | 分析与证据 |
| `/cases/{id}/applicant` | 申请信息 |
| `/cases/{id}/code` | 代码材料 |
| `/cases/{id}/materials` | 材料与导出 |
| `/cases/{id}/patent` | 专利交底书 |
| `/login` `/register` `/onboarding` `/account` `/admin` | 账号与引导 |

---

## 九、客户端不经过 HTTP

原生客户端（桌面 / Android）把同一批用例服务装进本机进程，**直接调用**，不经过上面这些端点：

| 端点 | 客户端对应调用 |
|---|---|
| `POST /api/cases/{id}/upload` | `DesktopEngine.ImportZipAsync(caseId, path, ct)` |
| `POST /api/cases/{id}/import-git` | `DesktopEngine.ImportGitAsync(caseId, url, branch, commit, ct)` |
| `POST /api/cases/{id}/scan` | `DesktopEngine.ScanAsync(caseId, ct)` |
| `GET/PUT /api/cases/{id}/applicant-info` | `DesktopEngine.GetApplicantAsync` / `SaveApplicantAsync` |
| `GET/PUT /api/cases/{id}/code-selection` | `DesktopEngine.ListCodeCandidatesAsync` / `SaveCodeSelectionAsync` |
| `POST /api/cases/{id}/confirm/{stage}` | `DesktopEngine.ConfirmAsync(caseId, stage, note, ct)` |
| `POST /api/cases/{id}/generate` | `DesktopEngine.GenerateAsync(caseId, ct)` |
| `GET /api/artifacts/{id}/download` | `DesktopEngine.ReadArtifactAsync(artifactId, ct)` |

两端的**校验规则、材料生成器、选项目录完全同一份实现**，不存在「客户端一套校验、服务器另一套校验」的问题。
