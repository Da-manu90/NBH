# NBH UI Design Audit: Carousel + Card Upgrades

Status: phased implementation in progress.

## Implementation Snapshot (Apr 2026)

- Done:
  - `ChatViewPage`: card upgrade + grouped sections (`Unread` / `Recent`)
  - `AddPage`: layered cards + featured carousel (`max 5`, snap, tap -> `DetailPage`)
  - `MapPage`: bottom-sheet card rail + card-to-map centering + active filter chip + reset action
  - `DetailPage`: hero card header, normalized card system, nested role cards with progress, compact applicant chips
  - `FAQPage`: accordion cards + category chip rail
  - `ProfilePage`: stat cards + activity carousel
- Remaining:
  - unify shared tokens as reusable style keys across all upgraded pages
  - acceptance checklist run on target Android devices + dark mode pass

## 1) Add/List Screen (`AddPage`)

- Replace flat list cards with layered cards:
  - top ribbon (`category`, `event_type`)
  - title + truncated body
  - metadata row (location, date, participants)
  - CTA chip ("Details")
- Add optional `CarouselView` for "featured entries" above list:
  - max 5 items
  - auto-snap, no auto-scroll
  - tap -> `DetailPage`
- Visual system:
  - radius: 18
  - shadow: low blur, low opacity
  - spacing rhythm: 8 / 12 / 16

## 2) Detail Screen (`DetailPage`)

- Header section:
  - convert to hero card with image fallback, owner avatar, rating badge.
- Content cards:
  - normalize all card paddings/margins to remove visual jumps.
  - convert action row to sticky bottom command bar.
- Group event section:
  - use nested cards per role with small progress bars.
  - move applicant actions into compact segmented action chips.

## 3) Map Screen (`MapPage`)

- Add bottom-sheet card rail for visible map entries:
  - horizontal `CarouselView` bound to current viewport pins.
  - selecting a card highlights/centers corresponding pin.
- Pin style consistency:
  - category-based color tokens (request/offer/event).
- Search/filter UX:
  - show active filter chip in header.
  - include reset action.

## 4) Chat Overview (`ChatViewPage`)

- Card upgrade for chat rows:
  - avatar + online indicator
  - title + last message preview
  - unread badge + relative timestamp
- Group by "Unread" / "Recent" sections for better scanning.

## 5) FAQ Screen (`FAQPage`)

- Replace plain expanders with accordion cards:
  - question as title
  - category pill
  - smooth expand animation
- Add optional category carousel chips at top.

## 6) Profile Screen (`ProfilePage`)

- Build profile stat cards in a two-column responsive grid.
- Introduce "activity carousel":
  - latest events/chats/notifications as compact cards.

## 7) Shared Card Design Tokens

- Radius: `18`
- Stroke color: `#12000000`
- Surface color: `#F8FFFD`
- Shadow:
  - opacity `0.12`
  - radius `10-14`
  - offset `0,4`
- Typography:
  - title 16/semibold
  - body 13/regular
  - meta 11/medium

## 8) Technical Rollout Order

1. `ChatViewPage` cards (low risk, high visual gain)
2. `AddPage` cards + featured carousel
3. `MapPage` bottom-sheet carousel sync
4. `DetailPage` action bar and group cards
5. `FAQPage` accordion cards
6. `ProfilePage` stat/activity cards

## 9) Acceptance Checklist

- No text clipping in DE locale.
- 60 FPS during list scroll on mid-range Android.
- Tap targets >= 44px.
- Dark mode readability pass.
- Empty/loading/error states use same card language.

### Current checklist status

- [ ] No text clipping in DE locale (manual device pass pending)
- [ ] 60 FPS during list scroll on mid-range Android (profiling pass pending)
- [x] Tap targets >= 44px (base styles enforce min 44; spot-check done on upgraded CTAs)
- [ ] Dark mode readability pass (final pass pending)
- [x] Empty/loading/error states use same card language (applied across upgraded screens)
