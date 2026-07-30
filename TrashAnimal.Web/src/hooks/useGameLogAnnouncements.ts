import { useEffect, useRef } from 'react';
import type { GameLogEntryView } from '../api/types';
import { useToast } from '../components/Toast/useToast';

/** A single re-fetch can surface several new entries at once (e.g. a block + turn-resolved
 * landing together); cap how many toasts fire per batch so the stack can't flood. */
const MAX_ANNOUNCEMENTS_PER_BATCH = 3;

/**
 * `GameEndedEvent`-derived entries are the one broadcast case in the A0 checklist (row 9): every
 * viewer — including the player whose turn emptied the deck — should hear "the game ended", not
 * just the players `AffectedPlayerSeat` would otherwise target. `GameLogEntryView` has no
 * structured event-type discriminator on the wire (see `GameLogProjector.BuildGameEndedMessage`
 * in `TrashAnimal/GameLog/GameLogProjector.cs`), so this is detected via a message fragment that
 * `BuildGameEndedMessage` always emits for every viewer regardless of win/lose framing —
 * `"— the game has ended! …"`. This is a narrow, deliberate exception for exactly one event type;
 * it is not the general "your"/"you" substring-matching approach the plan doc rejected for
 * selecting announcements generally (that job is done by `affectedPlayerSeat` instead).
 */
const GAME_ENDED_MESSAGE_MARKER = 'the game has ended!';

function isGameEndedEntry(entry: GameLogEntryView): boolean {
  return entry.message.includes(GAME_ENDED_MESSAGE_MARKER);
}

function isAnnounceable(entry: GameLogEntryView, localSeatIndex: number): boolean {
  if (isGameEndedEntry(entry)) {
    return true;
  }

  return entry.actingPlayerSeat !== localSeatIndex && entry.affectedPlayerSeat === localSeatIndex;
}

/**
 * Derives toast announcements from newly-arrived `GameLogEntryView` entries — frontend-only,
 * nothing added to the SignalR envelope (`GameHub` stays a push-only "go re-fetch" trigger; see
 * `TrashAnimal.Api/CLAUDE.md`). `SequenceNumber` is documented as stable and identical across all
 * viewers (`GameLogProjector`), so it is a reliable high-water mark for "what's genuinely new since
 * we last looked."
 *
 * On the very first render (a fresh mount, e.g. a page refresh mid-game), the high-water mark is
 * seeded from whatever entries are already present WITHOUT toasting any of them — otherwise a
 * refreshing player would get the entire backlog fired at them at once.
 */
export function useGameLogAnnouncements(entries: GameLogEntryView[], localSeatIndex: number): void {
  const { showToast } = useToast();
  const highWaterMarkRef = useRef<number | null>(null);

  useEffect(() => {
    if (entries.length === 0) {
      return;
    }

    const highestSequenceNumber = entries.reduce(
      (max, entry) => Math.max(max, entry.sequenceNumber),
      Number.NEGATIVE_INFINITY,
    );

    const previousHighWaterMark = highWaterMarkRef.current;
    if (previousHighWaterMark === null) {
      highWaterMarkRef.current = highestSequenceNumber;
      return;
    }

    if (highestSequenceNumber <= previousHighWaterMark) {
      return;
    }

    highWaterMarkRef.current = highestSequenceNumber;

    const newEntries = entries.filter((entry) => entry.sequenceNumber > previousHighWaterMark);
    const announceable = newEntries.filter((entry) => isAnnounceable(entry, localSeatIndex));
    const toAnnounce = announceable.slice(-MAX_ANNOUNCEMENTS_PER_BATCH);

    for (const entry of toAnnounce) {
      showToast(entry.message, 'info');
    }
  }, [entries, localSeatIndex, showToast]);
}
