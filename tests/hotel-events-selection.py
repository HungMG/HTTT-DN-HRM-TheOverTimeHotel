"""Browser check of calendar selection using local data; no database writes."""
import json
from pathlib import Path
from urllib.parse import parse_qs, urlparse
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parent.parent
web = root / 'src/OvertimeHotel.HRM.WebAdmin/wwwroot'
cards = [
    dict(id=1, name='Banquet', days=['2026-10-21', '2026-10-22']),
    dict(id=2, name='Banquet', days=['2026-10-22']),
]
for card in cards:
    card.update(time='09:00', type='Event', venue='Hall', count=2,
                status='KE_HOACH', statusLabel='Planned', url=f'/SuKien/Details?id={card["id"]}')
html = '''<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<style>''' + (web / 'css/events.css').read_text(encoding='utf-8') + '''</style></head>
<body><div class="events-page">
<div data-event-calendar data-year="2026" data-month="10" data-selected="2026-10-21"
 data-today="2026-10-21" data-index-url="/SuKien" data-create-url="/SuKien/Create">
<div data-calendar-day data-date="2026-10-21">21</div>
<div data-calendar-day data-date="2026-10-22">22</div></div>
<h3 data-selected-title></h3><p data-selected-count></p>
<div data-selected-events class="event-agenda-list"></div></div>
<script id="event-calendar-data" type="application/json">''' + json.dumps(cards) + '''</script>
<script>''' + (web / 'js/events.js').read_text(encoding='utf-8') + '''</script></body></html>'''

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page(viewport={'width': 1450, 'height': 1000})
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.route('http://hotel.test/**', lambda route: route.fulfill(content_type='text/html', body=html))
    page.goto('http://hotel.test/SuKien')
    agenda = page.locator('.event-agenda-card')
    assert agenda.count() == 1
    page.locator('[data-date="2026-10-22"]').click()
    assert agenda.count() == 3, 'Keep both days of a multi-day event and a separate same-name event'
    assert [item.split(' · ')[0] for item in agenda.locator('.event-eyebrow').all_text_contents()] == [
        '21/10/2026', '22/10/2026', '22/10/2026']
    assert [parse_qs(urlparse(href).query)['returnDate'][0] for href in agenda.evaluate_all('(cards)=>cards.map(c=>c.href)')] == [
        '2026-10-21', '2026-10-22', '2026-10-22']
    assert agenda.first.evaluate('(card)=>getComputedStyle(card).backgroundImage') == 'linear-gradient(135deg, rgb(15, 23, 42) 0%, rgb(30, 58, 138) 100%)'
    assert agenda.first.evaluate('(card)=>getComputedStyle(card).color') == 'rgb(255, 255, 255)'
    page.set_viewport_size({'width': 390, 'height': 844})
    assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
    page.locator('[data-date="2026-10-21"]').click()
    assert agenda.count() == 2
    page.locator('[data-date="2026-10-22"]').click()
    assert agenda.count() == 0
    assert page.locator('.event-empty').is_visible()
    assert not errors, errors
    browser.close()
print('PASS selected-day occurrences, same-name events, date links, navy cards, mobile and deselection')
