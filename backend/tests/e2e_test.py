#!/usr/bin/env python3
"""End-to-end smoke test for the CyberShield API. Usage: python3 e2e_test.py [base_url]"""
import sys, requests

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080"
PWD = "ChangeMe!123"
ok = fail = 0


def check(name, cond, extra=""):
    global ok, fail
    if cond:
        ok += 1; print(f"  PASS  {name}")
    else:
        fail += 1; print(f"  FAIL  {name} {extra}")


def call(method, path, token=None, **kw):
    h = {"Authorization": f"Bearer {token}"} if token else {}
    return requests.request(method, BASE + path, headers=h, timeout=20, **kw)


def login(email, pwd=PWD):
    r = call("POST", "/api/auth/login", json={"email": email, "password": pwd})
    return r, (r.json().get("token") if r.ok else None)


print("== Auth")
r, admin = login("m.vance@cybershield.internal")
check("admin login", r.status_code == 200 and admin)
r, analyst = login("e.rostova@cybershield.internal")
check("analyst login", r.status_code == 200 and analyst)
r, _ = login("m.vance@cybershield.internal", "wrong")
check("wrong password -> 401", r.status_code == 401)
r, _ = login("d.chen@cybershield.internal")
check("suspended user -> 403", r.status_code == 403)
r, _ = login("s.jenkins@cybershield.internal")
check("pending user -> 403", r.status_code == 403)
check("no token -> 401", call("GET", "/api/users").status_code == 401)
check("/me", call("GET", "/api/auth/me", admin).json()["role"] == "Administrator")

print("== Users")
r = call("GET", "/api/users", admin)
check("list users", r.status_code == 200 and r.json()["total"] == 6, r.text[:200])
check("filter role=Viewer", call("GET", "/api/users?role=Viewer", admin).json()["total"] == 1)
check("filter status=Suspended", call("GET", "/api/users?status=Suspended", admin).json()["items"][0]["fullName"] == "Devon Chen")
check("search", call("GET", "/api/users?search=rostova", admin).json()["total"] == 1)
check("bad enum -> 400", call("GET", "/api/users?role=Nope", admin).status_code == 400)
s = call("GET", "/api/users/summary", admin).json()
check("user summary", s["totalIdentities"] == 6 and s["administrators"] == 1, s)
r = call("POST", "/api/users", admin, json={"fullName": "Test User", "email": "t.user@cybershield.internal", "role": "Viewer"})
check("create user (admin)", r.status_code == 201 and len(r.json()["temporaryPassword"]) == 16, r.text[:200])
uid = r.json()["user"]["id"]
check("new user is Pending", r.json()["user"]["status"] == "Pending")
check("duplicate email -> 409", call("POST", "/api/users", admin, json={"fullName": "Dup", "email": "T.User@cybershield.internal", "role": "Viewer"}).status_code == 409)
check("invalid email -> 400", call("POST", "/api/users", admin, json={"fullName": "Bad", "email": "nope", "role": "Viewer"}).status_code == 400)
check("analyst cannot create user -> 403", call("POST", "/api/users", analyst, json={"fullName": "X Y", "email": "x@y.zz", "role": "Viewer"}).status_code == 403)
r = call("PATCH", f"/api/users/{uid}/status", admin, json={"status": "Active"})
check("activate user", r.status_code == 200 and r.json()["status"] == "Active")
tmp = call("POST", f"/api/users/{uid}/reset-password", admin).json()["temporaryPassword"]
r, viewer = login("t.user@cybershield.internal", tmp)
check("new user can login with temp password", r.status_code == 200 and viewer)
check("viewer cannot list users -> 403", call("GET", "/api/users", viewer).status_code == 403)
r = call("PUT", f"/api/users/{uid}", admin, json={"fullName": "Test User II", "role": "SecurityAnalyst", "mfaEnabled": True, "permissions": ["SIEM:RD"]})
check("update user", r.status_code == 200 and r.json()["role"] == "SecurityAnalyst" and r.json()["permissions"] == ["SIEM:RD"], r.text[:200])

print("== Devices")
r = call("GET", "/api/devices?pageSize=5", viewer)
check("list devices (viewer can read)", r.status_code == 200 and r.json()["total"] == 10, r.text[:200])
check("filter risk=Critical", call("GET", "/api/devices?risk=Critical", viewer).json()["total"] == 3)
check("sort=risk first is Critical", call("GET", "/api/devices?sort=risk", viewer).json()["items"][0]["riskLevel"] == "Critical")
check("search by ip", call("GET", "/api/devices?search=10.142.50.12", viewer).json()["total"] == 1)
ds = call("GET", "/api/devices/summary", viewer).json()
check("device summary", ds["totalEndpoints"] == 10 and ds["quarantinedNodes"] == 1 and len(ds["osDistribution"]) == 3, ds)
check("viewer cannot create device -> 403", call("POST", "/api/devices", viewer, json={"name": "X", "ipAddress": "1.1.1.1", "riskLevel": "Low", "status": "Online"}).status_code == 403)
r = call("POST", "/api/devices", analyst, json={"name": "TEST-HOST-01", "segment": "QA", "ipAddress": "10.9.9.9", "operatingSystem": "Ubuntu 24.04", "riskLevel": "Medium", "status": "Online"})
check("create device", r.status_code == 201, r.text[:200])
did = r.json()["id"]
check("duplicate device -> 409", call("POST", "/api/devices", analyst, json={"name": "TEST-HOST-01", "ipAddress": "10.9.9.8", "riskLevel": "Low", "status": "Online"}).status_code == 409)
check("isolate device", call("POST", f"/api/devices/{did}/isolate", analyst).json()["status"] == "Quarantined")
check("release device", call("POST", f"/api/devices/{did}/release", analyst).json()["status"] == "Online")
check("update device", call("PUT", f"/api/devices/{did}", analyst, json={"name": "TEST-HOST-01", "ipAddress": "10.9.9.9", "riskLevel": "High", "status": "Online"}).json()["riskLevel"] == "High")
check("analyst cannot delete device -> 403", call("DELETE", f"/api/devices/{did}", analyst).status_code == 403)
check("admin deletes device", call("DELETE", f"/api/devices/{did}", admin).status_code == 204)
check("deleted device -> 404", call("GET", f"/api/devices/{did}", admin).status_code == 404)

print("== Threats")
check("list threats", call("GET", "/api/threats", viewer).json()["total"] == 6)
check("filter category", call("GET", "/api/threats?category=Brute%20Force", viewer).json()["total"] == 1)
ts = call("GET", "/api/threats/summary", viewer).json()
check("threat summary categories", len(ts["categories"]) == 6 and ts["totalEvents"] == 1428, ts["totalEvents"])
check("threat origins ranked", ts["origins"][0]["rank"] == 1 and len(ts["origins"]) >= 3, ts["origins"])
r = call("POST", "/api/threats", analyst, json={"category": "Phishing Campaigns", "name": "Test Threat", "severity": "High", "eventCount": 10, "trendPercent": 1.5, "status": "Active"})
check("create threat", r.status_code == 201, r.text[:200]); tid = r.json()["id"]
check("update threat", call("PUT", f"/api/threats/{tid}", analyst, json={"category": "Phishing Campaigns", "name": "Test Threat", "severity": "Low", "eventCount": 11, "trendPercent": 0, "status": "Mitigated"}).json()["status"] == "Mitigated")
check("negative events -> 400", call("POST", "/api/threats", analyst, json={"category": "X", "name": "Y", "severity": "Low", "eventCount": -5, "trendPercent": 0, "status": "Active"}).status_code == 400)
check("delete threat", call("DELETE", f"/api/threats/{tid}", admin).status_code == 204)

print("== Alerts")
al = call("GET", "/api/alerts", viewer).json()
check("list alerts", al["total"] == 9, al["total"])
check("filter severity=Critical", all(a["severity"] == "Critical" for a in call("GET", "/api/alerts?severity=Critical", viewer).json()["items"]))
check("filter status=Blocked", call("GET", "/api/alerts?status=Blocked", viewer).json()["total"] == 2)
check("range=24h", call("GET", "/api/alerts?range=24h", viewer).json()["total"] == 9)
check("search signature", call("GET", "/api/alerts?search=Cobalt", viewer).json()["total"] == 1)
asum = call("GET", "/api/alerts/summary", viewer).json()
check("alert summary", asum["activeAlerts"] == 7 and asum["autoBlocked"] == 2, asum)
first = next(a for a in al["items"] if a["code"] == "ALT-9821")
det = call("GET", f"/api/alerts/{first['id']}", viewer).json()
check("alert detail with 3 IOCs", len(det["iocs"]) == 3 and det["alert"]["code"] == "ALT-9821")
r = call("POST", "/api/alerts", analyst, json={"signature": "Test Alert", "details": "d", "severity": "High", "sourceIp": "1.2.3.4", "destinationTarget": "host-x", "iocs": [{"type": "IP", "value": "1.2.3.4"}]})
check("create alert w/ auto code", r.status_code == 201 and r.json()["alert"]["code"] == "ALT-9822", r.text[:200])
aid = r.json()["alert"]["id"]
check("bad deviceId -> 400", call("POST", "/api/alerts", analyst, json={"signature": "T", "severity": "Low", "sourceIp": "1", "destinationTarget": "t", "deviceId": 99999}).status_code == 400)
check("set status", call("PATCH", f"/api/alerts/{aid}/status", analyst, json={"status": "Investigating"}).json()["status"] == "Investigating")
check("assign alert", call("POST", f"/api/alerts/{aid}/assign", analyst, json={"userId": call('GET','/api/auth/me',analyst).json()['id']}).json()["assignedToName"] == "Elena Rostova")
check("add IOC", call("POST", f"/api/alerts/{aid}/iocs", analyst, json={"type": "Hash", "value": "abc"}).status_code == 200)
inc = call("POST", f"/api/alerts/{aid}/escalate", analyst)
check("escalate -> incident", inc.status_code == 201 and inc.json()["code"] == "INC-8922", inc.text[:200])
check("escalate twice -> 409", call("POST", f"/api/alerts/{aid}/escalate", analyst).status_code == 409)
check("isolate host w/o device -> 400", call("POST", f"/api/alerts/{aid}/isolate", analyst).status_code == 400)
ws = next(a for a in al["items"] if a["code"] == "ALT-9820")
check("isolate host quarantines device", call("POST", f"/api/alerts/{ws['id']}/isolate", analyst).json()["status"] == "Blocked"
      and call("GET", f"/api/devices/{ws['deviceId']}", viewer).json()["status"] == "Quarantined")
check("dismiss", call("POST", f"/api/alerts/{aid}/dismiss", analyst).json()["status"] == "Dismissed")

print("== Incidents")
il = call("GET", "/api/incidents", viewer).json()
check("list incidents", il["total"] == 7, il["total"])
check("filter status=Open", call("GET", "/api/incidents?status=Open", viewer).json()["total"] == 2 + 1)
pipe = call("GET", "/api/incidents/pipeline", viewer).json()
check("pipeline columns", [c["status"] for c in pipe["columns"]] == ["Open", "Investigating", "Contained", "Resolved"], pipe["columns"])
check("pipeline KPIs", pipe["activeIncidents"] == 6 and pipe["mttrMinutes" if False else "meanTimeToResolveMinutes"] > 0, pipe["meanTimeToResolveMinutes"])
i8915 = next(i for i in il["items"] if i["code"] == "INC-8915")
d = call("GET", f"/api/incidents/{i8915['id']}", viewer).json()
check("incident detail (timeline 3, notes 2, mitre)", len(d["timeline"]) == 3 and len(d["notes"]) == 2 and d["mitreTechnique"].startswith("T1574"), d)
iid = inc.json()["id"]
check("assign analyst moves to Investigating", call("POST", f"/api/incidents/{iid}/assign", analyst, json={"userId": call('GET','/api/auth/me',analyst).json()['id']}).json()["status"] == "Investigating")
check("assign suspended user -> 400", call("POST", f"/api/incidents/{iid}/assign", analyst, json={"userId": next(u for u in call('GET','/api/users',admin).json()['items'] if u['status']=='Suspended')['id']}).status_code == 400)
check("add note", call("POST", f"/api/incidents/{iid}/notes", analyst, json={"body": "Looking into it."}).json()["authorName"] == "Elena Rostova")
check("add timeline", call("POST", f"/api/incidents/{iid}/timeline", analyst, json={"title": "Playbook run", "description": "Isolate"}).status_code == 201)
r = call("PATCH", f"/api/incidents/{iid}/status", analyst, json={"status": "Resolved"})
check("resolve sets resolvedAt", r.json()["status"] == "Resolved" and r.json()["resolvedAt"])
dd = call("GET", f"/api/incidents/{iid}", viewer).json()
check("timeline has status change", any(t["title"] == "Status changed" for t in dd["timeline"]) and len(dd["alertIds"]) == 1)
r = call("POST", "/api/incidents", analyst, json={"title": "Manual incident", "severity": "Medium"})
check("create incident", r.status_code == 201 and r.json()["incident"]["code"] == "INC-8923", r.text[:200])
check("viewer cannot create incident -> 403", call("POST", "/api/incidents", viewer, json={"title": "Nope", "severity": "Low"}).status_code in (403,))
check("delete incident (admin) detaches alerts", call("DELETE", f"/api/incidents/{iid}", admin).status_code == 204
      and call("GET", f"/api/alerts/{aid}", viewer).json()["alert"]["incidentId"] is None)

print("== Dashboard")
dsum = call("GET", "/api/dashboard/summary", viewer).json()
check("dashboard summary", 0 <= dsum["postureScore"] <= 100 and len(dsum["recentAlerts"]) == 4 and dsum["protectedDevices"] == 10, dsum)

print("== Cleanup / user delete")
check("cannot delete self", call("DELETE", f"/api/users/{call('GET','/api/auth/me',admin).json()['id']}", admin).status_code == 400)
check("delete assigned user (alerts un-assigned)", call("DELETE", f"/api/users/{uid}", admin).status_code == 204)
check("delete alert", call("DELETE", f"/api/alerts/{aid}", admin).status_code == 204)

print(f"\n{ok} passed, {fail} failed")
sys.exit(1 if fail else 0)
