// Native Node.js HTTP check. Uses only temporary records owned by this run.
// Run with PAYROLL_TEST_PASSWORD and optionally PAYROLL_TEST_USER/PAYROLL_BASE_URL.
import assert from "node:assert/strict";
const base = process.env.PAYROLL_BASE_URL || "http://localhost:5065";
const password = process.env.PAYROLL_TEST_PASSWORD;
assert(password, "Set PAYROLL_TEST_PASSWORD to an Admin/HR test account password.");
const cookies = new Map();
async function request(path, data, authenticated = true) {
    const response = await fetch(base + path, {
        method: data ? "POST" : "GET", redirect: "manual",
        headers: { ...(authenticated ? { Cookie: [...cookies].map(([k,v]) => k+"="+v).join("; ") } : {}),
            ...(data ? { "Content-Type": "application/x-www-form-urlencoded" } : {}) },
        body: data ? new URLSearchParams(data) : undefined
    });
    if (authenticated) for (const header of response.headers.getSetCookie()) {
        const pair = header.split(";")[0]; const equal = pair.indexOf("=");
        cookies.set(pair.slice(0,equal), pair.slice(equal+1));
    }
    return { status: response.status, location: response.headers.get("location"), html: (await response.text()).replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16))).replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n))) };
}
function token(html) {
    const input = html.match(/<input[^>]*name="__RequestVerificationToken"[^>]*>/)?.[0];
    const value = input?.match(/value="([^"]+)"/)?.[1];
    assert(value, "Antiforgery token is present."); return value;
}
function inputValue(html, name) {
    const input = html.match(new RegExp('<input[^>]*name="'+name+'"[^>]*>'))?.[0];
    return input?.match(/value="([^"]*)"/)?.[1];
}
let passed = 0;
function pass(name) { passed++; console.log("PASS " + name); }
const anonymous = await request("/CauHinhLuong", null, false);
assert.equal(anonymous.status, 302); assert.match(anonymous.location, /Account\/Login/);
pass("anonymous access requires login");
const login = await request("/Account/Login");
const signedIn = await request("/Account/Login", { Username: process.env.PAYROLL_TEST_USER || "admin",
    Password: password, __RequestVerificationToken: token(login.html) });
assert.equal(signedIn.status, 302, "Admin/HR test account can sign in.");
const categories = [
    ["TangCa", "", "1.50"], ["PhuCap", "CO_DINH_THANG", "35000.50"],
    ["KhauTru", "BAO_HIEM", "1.50"], ["Thuong", "", "200000.50"]
];
let expectedTotal = 0;
for (const [category] of categories) {
    const result = await request("/CauHinhLuong?category=" + category);
    assert.equal(result.status, 200);
    expectedTotal += (result.html.match(/<tr data-config-id=/g) || []).length;
}
function total(html) {
    const match = html.match(/>(\d+) cấu hình lương</);
    assert(match, "Dashboard reports a numeric payroll configuration total.");
    return Number(match[1]);
}
assert.equal(total((await request("/")).html), expectedTotal);
pass("dashboard total matches four catalog counts");
for (const [category, type, value] of categories) {
    const suffix = "?category=" + category;
    const list = await request("/CauHinhLuong" + suffix);
    assert.equal(list.status, 200); assert(!list.html.includes("Không thể tải dữ liệu"));
    pass(category + " reads real catalog");
    const form = await request("/CauHinhLuong/Create" + suffix);
    assert.equal(form.status, 200);
    const name = "SMOKE-" + category + "-" + Date.now();
    const fields = { Category: category, Name: name, Type: type, Value: value,
        Taxable: category === "PhuCap" ? "true" : "false", Description: "Temporary smoke test",
        __RequestVerificationToken: token(form.html) };
    const invalid = await request("/CauHinhLuong/Create", { ...fields, Value: "-1" });
    assert.equal(invalid.status, 200); assert.match(invalid.html, /field-validation-error/);
    pass(category + " rejects negative value");
    const missing = { ...fields }; delete missing.Value;
    const missingResult = await request("/CauHinhLuong/Create", missing);
    assert.equal(missingResult.status, 200); assert.match(missingResult.html, /field-validation-error/);
    for (const badValue of ["1.501", ...(category === "TangCa" ? ["0", "100"] : category === "KhauTru" ? ["101"] : [])]) {
        const rejected = await request("/CauHinhLuong/Create", { ...fields, Value: badValue });
        assert.equal(rejected.status, 200); assert.match(rejected.html, /field-validation-error/);
    }
    if (type) {
        const badType = await request("/CauHinhLuong/Create", { ...fields, Type: "INVALID" });
        assert.equal(badType.status, 200); assert.match(badType.html, /field-validation-error/);
    }
    pass(category + " rejects missing, over-precision and out-of-range values");
    const created = await request("/CauHinhLuong/Create", fields);
    assert.equal(created.status, 302);
    let id;
    try {
        const filtered = await request("/CauHinhLuong" + suffix + "&keyword=" + encodeURIComponent(name));
        const row = filtered.html.match(/<tr data-config-id="(\d+)">[\s\S]*?<\/tr>/);
        assert(row && row[0].includes(name), "New record is persisted and searchable.");
        id = row[1];
        if (category === "PhuCap") assert.match(row[0], /<td>Có<\/td>/);
        if (type) {
            const typed = await request("/CauHinhLuong" + suffix + "&type=" + type + "&keyword=" + encodeURIComponent(name));
            assert.equal(typed.status, 200); assert(typed.html.includes(name));
        }
        assert.equal(total((await request("/")).html), expectedTotal + 1);
        pass(category + " creates with decimals, checkbox, filter and refreshed dashboard");
        const duplicate = await request("/CauHinhLuong/Create", { ...fields, Name: name.toLowerCase() });
        assert.equal(duplicate.status, 200); assert.match(duplicate.html, /field-validation-error/);
        pass(category + " rejects duplicate name");
        const edit = await request("/CauHinhLuong/Edit" + suffix + "&id=" + id);
        assert.equal(edit.status, 200); assert.equal(Number(inputValue(edit.html, "Value")), Number(value));
        const saved = await request("/CauHinhLuong/Edit?id=" + id, { ...fields, Id: id,
            Name: name + "-updated", Value: category === "TangCa" ? "2.25" : "3.25",
            Taxable: "false", __RequestVerificationToken: token(edit.html) });
        assert.equal(saved.status, 302);
        const verified = await request("/CauHinhLuong/Edit" + suffix + "&id=" + id);
        assert.equal(inputValue(verified.html, "Name"), name + "-updated");
        assert.equal(Number(inputValue(verified.html, "Value")), category === "TangCa" ? 2.25 : 3.25);
        pass(category + " edits and persists decimals");
        assert.equal((await request("/CauHinhLuong/Delete" + suffix + "&id=" + id, {})).status, 400);
        pass(category + " rejects delete without antiforgery");
    } finally {
        if (id) {
            const cleanup = await request("/CauHinhLuong" + suffix);
            const deleted = await request("/CauHinhLuong/Delete" + suffix + "&id=" + id,
                { __RequestVerificationToken: token(cleanup.html) });
            assert.equal(deleted.status, 302);
            assert.equal((await request("/CauHinhLuong/Edit" + suffix + "&id=" + id)).status, 404);
            assert.equal(total((await request("/")).html), expectedTotal);
            pass(category + " deletes temporary record and refreshes dashboard");
        }
    }
    const used = list.html.match(/<tr data-config-id="(\d+)">(?:(?!<\/tr>)[\s\S])*Đang được sử dụng(?:(?!<\/tr>)[\s\S])*<\/tr>/);
    if (used) {
        const blocked = await request("/CauHinhLuong/Delete" + suffix + "&id=" + used[1],
            { __RequestVerificationToken: token(form.html) });
        assert.equal(blocked.status, 302);
        assert.equal((await request("/CauHinhLuong/Edit" + suffix + "&id=" + used[1])).status, 200);
        pass(category + " blocks deleting a referenced config");
    }
}
assert.equal((await request("/CauHinhLuong?category=999")).status, 400);
assert.equal((await request("/CauHinhLuong?category=KhauTru&type=INVALID")).status, 400);
assert.equal((await request("/CauHinhKhoanLuong")).status, 302);
const dashboard = await request("/");
assert.equal(dashboard.status, 200);
assert.match(dashboard.html, /cấu hình lương/);
assert.match(dashboard.html, /tăng ca/);
pass("dashboard, legacy redirect and invalid category filters");
console.log("Passed " + passed + " checks; temporary test records removed.");
