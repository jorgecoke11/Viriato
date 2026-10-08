/** An on/off control for something that takes effect right away (unlike a checkbox, which waits for a Save).
 *  Announced as a switch; the visible label, if any, says what it is and the state in words. */
export function Switch({
  checked,
  onChange,
  label,
  disabled,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  /** Accessible name, and the text shown next to the switch unless `hideLabel`. */
  label: string
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      title={label}
      className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border border-transparent ${
        checked ? 'bg-green-500' : 'bg-gray-300'
      } disabled:cursor-not-allowed disabled:opacity-50`}
      onClick={() => onChange(!checked)}
    >
      <span
        aria-hidden="true"
        className={`inline-block h-[18px] w-[18px] rounded-full bg-white shadow transition-transform ${checked ? 'translate-x-[22px]' : 'translate-x-[3px]'}`}
      />
    </button>
  )
}
