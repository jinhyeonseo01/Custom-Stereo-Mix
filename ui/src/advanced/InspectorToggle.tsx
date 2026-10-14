import { useId } from "react";
import { Power } from "../icons";
export function InspectorToggle({ label, hint, checked, disabled = false, primary = false, onChange }:
  { label: string; hint: string; checked: boolean; disabled?: boolean; primary?: boolean; onChange: (checked: boolean) => void }) {
  const hintId = useId();
  return <label title={hint} className={`inspector-toggle ${primary ? "activation-control" : ""} ${checked ? "is-on" : "is-off"} ${disabled ? "is-locked" : ""}`}>
    {primary && <Power size={23} />}
    <span className="inspector-toggle-copy"><strong>{label}</strong><small id={hintId}>{hint}</small></span>
    <input type="checkbox" role="switch" checked={checked} disabled={disabled} aria-label={label} aria-describedby={hintId} onChange={e => onChange(e.target.checked)} />
    <span className="switch-visual" aria-hidden="true" />
  </label>;
}
