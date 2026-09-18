/** Inspect all text, retaining only enough overlap to detect split values. */
export function createLogContentScanner(forbidden) {
  if (!forbidden.length || forbidden.some(value => !value.length))
    throw new Error("Log checks require nonempty forbidden values.");
  const overlap = Math.max(...forbidden.map(value => value.length)) - 1;
  const found = new Set();
  let tail = "";
  return {
    write(chunk) {
      const text = tail + chunk;
      for (const value of forbidden) if (text.includes(value)) found.add(value);
      tail = overlap ? text.slice(-overlap) : "";
    },
    violations: () => [...found],
  };
}
