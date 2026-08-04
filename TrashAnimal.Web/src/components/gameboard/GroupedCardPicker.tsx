import { useState } from 'react';
import type { CardName } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';

const CARDS_PER_ROW = 3;

interface PickableCard {
  cardId: string;
  name: CardName;
}

interface GroupedCardPickerProps {
  cards: PickableCard[];
  min: number;
  max: number;
  isPending: boolean;
  onConfirm: (cardIds: string[]) => void;
  confirmLabelPrefix?: string;
}

/** Groups a flat card list by name and renders +/− per-group counters capped by `max`.
 * Always requires an explicit confirm click — no auto-submit on `+`, even at min===max===1. */
function GroupedCardPicker({
  cards,
  min,
  max,
  isPending,
  onConfirm,
  confirmLabelPrefix = 'Stash',
}: GroupedCardPickerProps) {
  const groupedIds = new Map<CardName, string[]>();
  for (const card of cards) {
    const existing = groupedIds.get(card.name) ?? [];
    existing.push(card.cardId);
    groupedIds.set(card.name, existing);
  }

  const [selected, setSelected] = useState<Record<string, number>>(() => {
    const initial: Record<string, number> = {};
    for (const name of groupedIds.keys()) initial[name] = 0;
    return initial;
  });

  const totalSelected = Object.values(selected).reduce((sum, n) => sum + n, 0);

  const entries = [...groupedIds.entries()];
  const rows: (typeof entries)[] = [];
  for (let i = 0; i < entries.length; i += CARDS_PER_ROW) {
    rows.push(entries.slice(i, i + CARDS_PER_ROW));
  }

  function increment(name: CardName) {
    const groupSize = groupedIds.get(name)?.length ?? 0;
    if (totalSelected >= max || (selected[name] ?? 0) >= groupSize) return;
    setSelected((prev) => ({ ...prev, [name]: (prev[name] ?? 0) + 1 }));
  }

  function decrement(name: CardName) {
    if ((selected[name] ?? 0) <= 0) return;
    setSelected((prev) => ({ ...prev, [name]: (prev[name] ?? 0) - 1 }));
  }

  function handleConfirm() {
    const cardIds: string[] = [];
    for (const [name, count] of Object.entries(selected)) {
      const ids = groupedIds.get(name as CardName) ?? [];
      cardIds.push(...ids.slice(0, count));
    }
    onConfirm(cardIds);
    setSelected((prev) => Object.fromEntries(Object.keys(prev).map((k) => [k, 0])));
  }

  return (
    <div className="flex flex-col gap-3">
      <div
        className="flex max-h-[60vh] flex-col gap-4 overflow-y-auto phone-landscape:max-h-[calc(100vh-220px)]"
        style={{ scrollSnapType: 'y mandatory' }}
      >
        {rows.map((row, rowIndex) => (
          <div key={rowIndex} className="flex gap-4" style={{ scrollSnapAlign: 'start' }}>
            {row.map(([name, ids]) => {
              const count = selected[name] ?? 0;
              const canIncrement = count < ids.length && totalSelected < max;
              return (
                <div key={name} className="flex flex-col items-center gap-1">
                  <div className="relative">
                    <img
                      src={CARD_IMAGE_BY_NAME[name]}
                      alt={name}
                      className="h-[120px] w-[86px] rounded-lg object-cover"
                      style={{ opacity: 1 }}
                    />
                    <span
                      className="absolute -right-2 -top-2 flex h-6 w-6 items-center justify-center rounded-full border-2 text-[10px] font-bold"
                      style={{
                        background: 'var(--gb-gold)',
                        color: 'var(--gb-gold-text-dark)',
                        borderColor: 'var(--gb-gold-text-dark)',
                      }}
                    >
                      {ids.length}
                    </span>
                  </div>
                  <p className="text-[11px] font-medium" style={{ color: 'var(--gb-text-primary)' }}>
                    {name}
                  </p>
                  <p className="text-[10px]" style={{ color: 'var(--gb-text-label)' }}>
                    {count} / {ids.length}
                  </p>
                  <div className="flex gap-1">
                    <button
                      type="button"
                      onClick={() => decrement(name)}
                      disabled={isPending || count <= 0}
                      aria-label={`Remove ${name}`}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-sm font-bold disabled:opacity-40"
                      style={{ background: 'rgba(255,255,255,.22)', border: '1.5px solid rgba(255,255,255,.5)', color: 'var(--gb-text-primary)' }}
                    >
                      −
                    </button>
                    <button
                      type="button"
                      onClick={() => increment(name)}
                      disabled={isPending || !canIncrement}
                      aria-label={`Add ${name}`}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-sm font-bold disabled:opacity-40"
                      style={{ background: 'rgba(255,255,255,.22)', border: '1.5px solid rgba(255,255,255,.5)', color: 'var(--gb-text-primary)' }}
                    >
                      +
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        ))}
      </div>
      <button
        type="button"
        disabled={isPending || totalSelected < min}
        onClick={handleConfirm}
        className="self-start rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
        style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
      >
        {confirmLabelPrefix} {totalSelected} card{totalSelected === 1 ? '' : 's'}
      </button>
    </div>
  );
}

export default GroupedCardPicker;
