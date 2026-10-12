#!/usr/bin/env python3
"""Browser test: Alerts page wired to the live API. Usage: python3 ui_alerts_test.py [base_url]"""
import sys, re
from playwright.sync_api import sync_playwright

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080"
ok = fail = 0


def check(name, cond, extra=""):
    global ok, fail
    if cond: ok += 1; print("  PASS ", name)
    else: fail += 1; print("  FAIL ", name, extra)


with sync_playwright() as p:
    b = p.chromium.launch()
    ctx = b.new_context(viewport={"width": 1440, "height": 900})
    page = ctx.new_page()
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    dialogs = []
    page.on("dialog", lambda d: (dialogs.append(d.message), d.accept()))

    page.goto(BASE + "/alerts.html")
    page.wait_for_selector("#cs-login", timeout=8000)
    check("login overlay shown when signed out", True)

    # wrong password
    page.fill("#cs-email", "m.vance@cybershield.internal"); page.fill("#cs-pass", "nope")
    page.click("#cs-login-btn")
    page.wait_for_function("document.querySelector('#cs-login-err').textContent.length>0")
    check("wrong password shows error", "Invalid" in page.inner_text("#cs-login-err"))

    page.fill("#cs-pass", "ChangeMe!123"); page.click("#cs-login-btn")
    page.wait_for_selector("#alerts-body tr[data-id]", timeout=8000)
    check("overlay removed after login", page.locator("#cs-login").count() == 0)
    check("user chip shows name", "Marcus Vance" in page.inner_text("#cs-user-chip"))
    rows = page.locator("#alerts-body tr[data-id]").count()
    check("alert rows rendered from API (9 seeded)", rows == 9, rows)
    page.wait_for_function("document.getElementById('st-active').textContent.trim()!=='1,284'", timeout=5000)
    check("stat cards come from API", page.inner_text("#st-critical").strip() != "48" and page.inner_text("#st-active").strip() != "1,284",
          (page.inner_text("#st-active"), page.inner_text("#st-critical")))
    check("pager text", "of 9 alerts" in page.inner_text("#pg-info"), page.inner_text("#pg-info"))
    page.screenshot(path="/tmp/ui_alerts_list.png")

    # filters
    page.select_option("#f-sev", "critical"); page.wait_for_timeout(700)
    n = page.locator("#alerts-body tr[data-id]").count()
    check("severity filter -> only Critical", n >= 1 and all("Critical" in t for t in page.locator("#alerts-body tr[data-id] td:nth-child(2)").all_inner_texts()), n)
    page.select_option("#f-sev", "")
    page.fill("#f-search", "Cobalt"); page.wait_for_timeout(900)
    check("search filter", page.locator("#alerts-body tr[data-id]").count() == 1)
    page.fill("#f-search", "zzzz-nothing"); page.wait_for_timeout(900)
    check("empty state", "No alerts match" in page.inner_text("#alerts-body"))
    page.fill("#f-search", ""); page.wait_for_timeout(900)
    page.select_option("#f-status", "blocked"); page.wait_for_timeout(700)
    check("status filter -> Blocked", page.locator("#alerts-body tr[data-id]").count() >= 1 and
          all("Blocked" in t for t in page.locator("#alerts-body tr[data-id] td:nth-child(6)").all_inner_texts()))
    page.select_option("#f-status", ""); page.wait_for_timeout(700)

    # detail panel
    page.fill("#f-search", "Cobalt"); page.wait_for_timeout(900)
    page.click("#alerts-body tr[data-id]")
    page.wait_for_function("document.getElementById('slideOverPanel').dataset.alert==='ALT-9821'", timeout=5000)
    check("panel title from API", "Cobalt Strike" in page.inner_text("#so-title"))
    check("panel shows 3 IOCs", page.locator("#so-iocs > div").count() == 3)
    check("playbook generated", page.locator("#so-playbook li").count() >= 3)
    check("hex dump hidden in live mode", not page.locator("#so-hex").is_visible())
    check("isolate disabled (no linked device)", page.locator("#so-isolate").is_disabled())
    page.screenshot(path="/tmp/ui_alerts_panel.png")

    page.click("#so-assign"); page.wait_for_function("document.getElementById('so-assignee').textContent==='Marcus Vance'", timeout=4000)
    check("assign to me", True)

    page.click("#so-escalate")
    page.wait_for_selector("#cs-toasts div:has-text('escalated')", timeout=4000)
    toast = page.inner_text("#cs-toasts")
    check("escalate -> incident toast", re.search(r"escalated to incident INC-\d+", toast) is not None, toast)
    page.wait_for_timeout(900)
    page.fill("#f-search", ""); page.wait_for_timeout(900)

    # dismiss another
    page.fill("#f-search", "Replica Lag"); page.wait_for_timeout(900)
    page.click("#alerts-body tr[data-id]"); page.wait_for_function("document.getElementById('slideOverPanel').dataset.alert==='ALT-9813'", timeout=5000)
    check("resolved alert has dismiss disabled", page.locator("#so-dismiss").is_disabled())
    page.click("text=Close Inspection"); page.fill("#f-search", ""); page.wait_for_timeout(900)

    # bulk isolate needs selection
    page.click("#btn-bulk"); page.wait_for_selector("#cs-toasts div:has-text('Select at least one')", timeout=3000)
    check("bulk isolate without selection warns", True)
    page.locator("#alerts-body tr[data-id] .row-chk").nth(1).check()
    page.click("#btn-bulk"); page.wait_for_timeout(1500)
    check("bulk isolate asks for confirmation", any("selected alert" in d for d in dialogs), dialogs)

    # export (downloads csv)
    with page.expect_download(timeout=8000) as dl:
        page.click("#btn-export")
    check("export IOCs downloads csv", dl.value.suggested_filename == "cybershield-iocs.csv")

    # sign out then login again persists token across reload
    page.reload(); page.wait_for_selector("#alerts-body tr[data-id]", timeout=8000)
    check("session persists across reload", page.locator("#cs-login").count() == 0)
    page.click("#cs-signout"); page.wait_for_selector("#cs-login", timeout=8000)
    check("sign out returns to login", True)

    # mobile layout
    m = b.new_context(viewport={"width": 390, "height": 844}).new_page()
    m.goto(BASE + "/alerts.html"); m.wait_for_selector("#cs-login")
    m.fill("#cs-email", "e.rostova@cybershield.internal"); m.fill("#cs-pass", "ChangeMe!123"); m.click("#cs-login-btn")
    m.wait_for_selector("#alerts-body tr[data-id]", timeout=8000)
    m.screenshot(path="/tmp/ui_alerts_mobile.png")
    check("no horizontal page overflow on mobile", m.evaluate("document.documentElement.scrollWidth <= window.innerWidth + 2"))

    check("no JS errors", not errors, errors)
    b.close()

print(f"\n{ok} passed, {fail} failed")
sys.exit(1 if fail else 0)
