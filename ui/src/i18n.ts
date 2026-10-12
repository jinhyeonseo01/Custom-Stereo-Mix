import english from "../../locales/en-US.json";
export type MessageKey = keyof typeof english.messages;
interface Locale {
  code: string;
  name: string;
  order: number;
  messages: Partial<Record<MessageKey, string>>;
}
// Adding another JSON resource automatically registers it in both frontends.
export const languages = Object.values(
  import.meta.glob<Locale>("../../locales/*.json", {
    eager: true,
    import: "default",
  }),
).sort((a, b) => a.order - b.order);
function resolve(code?: string): string {
  return (
    languages.find((l) => l.code === code)?.code ??
    languages.find((l) => l.code.split("-")[0] === code?.split("-")[0])?.code ??
    "en-US"
  );
}
let language = resolve(window.__initialLanguage ?? navigator.language);
document.documentElement.lang = language;
export function setLanguage(code?: string) {
  if (!code) return;
  language = resolve(code);
  document.documentElement.lang = language;
}
export function t(key: MessageKey, ...args: Array<string | number>): string {
  const selected = languages.find((l) => l.code === language)!;
  const text = selected.messages[key] ?? english.messages[key];
  return text.replace(/\{(\d+)\}/g, (match, index: string) =>
    String(args[Number(index)] ?? match),
  );
}
