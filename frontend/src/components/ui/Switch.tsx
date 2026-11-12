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
      className={`group relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border border-transparent focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 ${
        checked ? 'bg-green-500' : 'bg-gray-300'
      } disabled:cursor-not-allowed disabled:opacity-50`}
      onClick={() => onChange(!checked)}
    >
      <span
        aria-hidden="true"
        className={`inline-block h-[18px] w-[18px] rounded-full bg-white shadow transition-[translate,width] duration-200 ease-[cubic-bezier(0.16,1,0.3,1)] group-enabled:group-active:w-[22px] ${
          checked ? 'translate-x-[22px] group-enabled:group-active:translate-x-[18px]' : 'translate-x-[3px]'
        }`}
      />
    </button>
  )
}
