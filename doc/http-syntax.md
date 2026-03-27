# JetBrains HTTP 文件语法总结

> 参考来源：[Exploring the HTTP request syntax](https://www.jetbrains.com.cn/en-us/help/idea/exploring-http-syntax.html) · [HTTP Client variables](https://www.jetbrains.com.cn/en-us/help/idea/http-client-variables.html)

---

## 1. 基本语法结构

```
Method Request-URI HTTP-Version
Header-field: Header-value

Request-Body
```

**示例：**

```http
POST https://example.com:8080/api/html/post HTTP/1.1
Content-Type: application/json
Cookie: key=first-value

{ "key": "value", "list": [1, 2, 3] }
```

---

## 2. 注释

在请求中，以 `//` 或 `#` 开头的行为注释行：

```http
// 这是一个注释
# 这也是一个注释
GET http://example.com/api
```

---

## 3. 请求命名

在请求上方使用 `### 名称`、`# @name` 或 `# @name =` 为请求命名，便于在搜索和运行配置中定位：

```http
### Get all users
GET http://example.com/users

# @name createUser
POST http://example.com/users
Content-Type: application/json

{ "name": "Alice" }
```

---

## 4. 单文件多请求

使用 `###` 分隔符将多个请求写在同一个 `.http` 文件中：

```http
// 第一个请求
GET https://example.com/a/

###

// 第二个请求
GET https://example.com:8080/api/html/get?id=123&value=content
```

---

## 5. GET 请求简写

GET 请求可以省略方法名，只写 URI：

```http
// 完整写法
GET https://example.com/a/

###

// 简写（等价）
https://example.com/a/
```

---

## 6. 长请求 URL 换行

对过长的 URL，可以缩进换行（除第一行外所有行须缩进）：

```http
GET http://example.com:8080
    /api
    /html
    /get
    ?id=123
    &value=content
```

---

## 7. 身份认证

```http
// Basic 认证
GET http://example.com
Authorization: Basic username password

###

// Digest 认证
GET http://example.com
Authorization: Digest username password
```

> `username` 和 `password` 支持使用环境变量参数化，例如 `{{username}}`。

---

## 8. 请求体（Request Body）

请求体与请求头之间须有一个空行。

### 8.1 内联请求体

```http
POST https://example.com/api
Content-Type: application/json

{ "key": "value", "list": [1, 2, 3] }
```

### 8.2 从文件读取请求体

```http
POST https://example.com/api
Content-Type: application/json

< ./input.json
```

### 8.3 multipart/form-data

```http
POST https://example.com/api/upload HTTP/1.1
Content-Type: multipart/form-data; boundary=boundary

--boundary
Content-Disposition: form-data; name="first"; filename="input.txt"

< ./input.txt

--boundary
Content-Disposition: form-data; name="second"; filename="input-second.txt"

Text content here
--boundary--
```

---

## 9. 请求行为注解（Annotations）

在请求前以注释行添加标签以控制请求行为：

| 注解 | 说明 |
|------|------|
| `// @no-redirect` | 禁止自动跟随重定向，返回原始 3xx 响应头 |
| `// @no-log` | 禁止将该请求保存到历史记录（适合含敏感数据的请求） |
| `// @no-cookie-jar` | 禁止将响应中的 Cookie 保存到 Cookie Jar |
| `// @no-auto-encoding` | 禁止自动 URL 编码（请求参数和 Body 原样发送） |
| `# @timeout 600` | 设置等待新数据包的超时时间（默认单位秒，支持 `ms`/`s`/`m`） |
| `// @connection-timeout 2 m` | 设置建立连接的超时时间 |

**示例：**

```http
// @no-redirect
GET example.com/status/301

###

// @no-log
GET example.com/api/sensitive

###

# @timeout 600
GET example.com/slow-api

###

// @connection-timeout 2 m
GET example.com/api
```

---

## 10. 执行延迟

使用 `sleep(ms)` 配合 `await` 关键字暂停执行（适用于 WebSocket 测试、连接测试等场景）：

```http
< {%
    console.log("Pre-request: Starting sleep...");
    const start = Date.now();
    await sleep(3000);
    console.log(`Pre-request: Finished sleep after ${Date.now() - start}ms`);
%}

GET https://example.com/api
```

---

## 11. 变量（Variables）

### 11.1 基本使用

用双大括号 `{{variableName}}` 引用变量，可用于 URL、请求头、请求体等任何位置：

```http
GET http://{{host}}/api/json/get?id={{id-value}}
Authorization: Basic {{username}} {{password}}
Content-Type: application/json

{ "key": "{{my-var}}" }
```

变量名可包含字母、数字、`_`、`-`、`.`（含点号时系统将其解析为 JSONPath 表达式）。

### 11.2 变量类型及优先级（从高到低）

| 类型 | 定义位置 | 作用域 |
|------|----------|--------|
| **环境变量** (Environment) | `http-client.env.json` / `http-client.private.env.json` | 整个项目的 `.http` 文件 |
| **全局变量** (Global) | 响应处理/预请求脚本中 `client.global.set()` | 跨请求持久化 |
| **文件内变量** (In-place) | `.http` 文件顶部 `@name = value` | 同一 `.http` 文件内 |
| **请求级变量** (Per-request) | 预请求脚本中 `request.variables.set()` | 仅当前单个请求 |

### 11.3 环境变量文件

**`http-client.env.json`**（公共，可提交 Git）：

```json
{
    "development": {
        "host": "localhost",
        "id-value": 12345,
        "username": "dev-user",
        "password": ""
    },
    "production": {
        "host": "example.com",
        "id-value": 6789,
        "username": "",
        "password": ""
    }
}
```

**`http-client.private.env.json`**（私有，不应提交 Git）：

```json
{
    "development": {
        "password": "my-secret-dev-password"
    }
}
```

> 私有文件中同名变量的值会覆盖公共文件中的值。

### 11.4 文件内变量（In-place Variables）

在文件顶部使用 `@` 定义，作用域为整个文件：

```http
@baseUrl = https://api.example.com
@token = my-dev-token

GET {{baseUrl}}/users
Authorization: Bearer {{token}}

###

GET {{baseUrl}}/stats
Authorization: Bearer {{token}}
```

### 11.5 请求级变量（Per-request Variables）

在预请求脚本中设置，仅对紧跟其后的单个请求生效：

```http
< {%
    request.variables.set("firstname", "John")
%}
GET http://example.org/{{firstname}}
```

### 11.6 动态变量（Dynamic Variables）

动态变量每次执行时生成新值，名称以 `$` 开头：

| 变量 | 说明 |
|------|------|
| `{{$uuid}}` / `{{$random.uuid}}` | 生成 UUID-v4 |
| `{{$timestamp}}` | 当前 Unix 时间戳 |
| `{{$isoTimestamp}}` | 当前 ISO-8601 格式时间（UTC） |
| `{{$randomInt}}` | 0~1000 之间的随机整数 |
| `{{$random.integer(from, to)}}` | 指定范围的随机整数 |
| `{{$random.float(from, to)}}` | 指定范围的随机浮点数 |
| `{{$random.alphabetic(length)}}` | 指定长度的随机字母串 |
| `{{$random.alphanumeric(length)}}` | 指定长度的随机字母+数字+下划线串 |
| `{{$random.hexadecimal(length)}}` | 指定长度的随机十六进制串 |
| `{{$random.email}}` | 随机邮箱地址 |

```http
POST http://localhost/api/post?id={{$uuid}}

{
    "time": {{$timestamp}},
    "price": {{$random.integer(10, 1000)}}
}
```

### 11.7 系统环境变量

使用 `{{$env.ENV_VAR}}` 语法访问操作系统环境变量：

```http
GET http://localhost:63345/{{$env.USERNAME}}
```

### 11.8 集合变量（遍历请求）

变量值可以是数组，HTTP Client 会为每个元素发送一个独立请求：

```http
< {%
    request.variables.set("id", [1, 2, 3, 4, 5])
%}
GET http://localhost:8080/books/{{id}}
```

也支持 JSONPath 访问数组对象中的字段：

```http
GET http://localhost:8080/users/{{users[*].name}}
```

---

## 12. 预请求脚本（Pre-request Scripts）

在请求前使用 `< {% ... %}` 包裹 JavaScript 代码，可用于设置变量、计算签名等：

```http
< {%
    const token = client.global.get("auth_token");
    request.variables.set("token", token);
%}
GET https://example.com/api/protected
Authorization: Bearer {{token}}
```

---

## 13. 响应处理（Response Handler）

在请求后使用 `> 文件路径` 或内联的 `> {% ... %}` 处理响应：

```http
// 外部脚本文件
GET https://httpbin.org/get

> /path/to/responseHandler.js
```

```http
// 内联脚本
GET https://httpbin.org/get

> {%
    client.global.set("my_cookie", response.headers.valuesOf("Set-Cookie")[0]);
    client.test("Status is 200", function() {
        client.assert(response.status === 200, "Response status is not 200");
    });
%}
```

---

## 14. 响应重定向（Response Redirect）

将响应保存到文件：

| 语法 | 说明 |
|------|------|
| `>> path/to/file.json` | 若文件存在则自动创建带后缀的新文件（如 `file-1.json`） |
| `>>! path/to/file.json` | 若文件存在则直接覆盖 |

内置路径变量：

- `{{$projectRoot}}` — 项目根目录
- `{{$historyFolder}}` — `.idea/httpRequests/` 目录

```http
POST https://httpbin.org/post
Content-Type: application/json

{ "id": 999, "value": "content" }

>> myFolder/myFile.json
```

```http
POST https://httpbin.org/post
Content-Type: application/json

{ "id": 999 }

> {{$projectRoot}}/handler.js

>>! {{$historyFolder}}/myFile.json
```

---

## 完整综合示例

```http
@baseUrl = https://api.example.com
@token = my-secret-token

### 获取用户列表
# @name getUsers
GET {{baseUrl}}/users
Authorization: Bearer {{token}}
Accept: application/json

> {%
    client.global.set("first_user_id", response.body.data[0].id);
%}

###

### 获取指定用户（不存历史）
# @name getUser
// @no-log
GET {{baseUrl}}/users/{{first_user_id}}
Authorization: Bearer {{token}}

###

### 创建用户（30s 超时，响应存文件）
# @timeout 30
POST {{baseUrl}}/users
Content-Type: application/json
Authorization: Bearer {{token}}

{
    "id": "{{$uuid}}",
    "name": "Alice",
    "created_at": "{{$isoTimestamp}}"
}

>> ./responses/create-user.json

###

### 批量查询（遍历集合）
< {%
    request.variables.set("userId", [1, 2, 3])
%}
GET {{baseUrl}}/users/{{userId}}
Authorization: Bearer {{token}}
```
