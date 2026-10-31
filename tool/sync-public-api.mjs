// Spec 0002 LIB-10, spec 0014 RGN-2: writes what the generator changed into PublicAPI.Unshipped.txt, so a regenerate PR
// builds under CI's warnings as errors. The analyzer names each symbol in its RS0016 (new) and RS0017 (gone) messages,
// and those messages are the line to add or remove. `dotnet format` doesn't apply this analyzer's fixer. tool/regenerate.sh
// runs this after the generator; a hand written public change goes through it too, so the diff still shows every change.
import { spawnSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const project = join(import.meta.dirname, "..", "src", "Orvano.Server");
const unshipped = join(project, "PublicAPI.Unshipped.txt");
const header = "#nullable enable";

// CI=false keeps these as warnings, so the build finishes and reports all of them. MSBuild reads GitHub's CI=true from
// the environment, which is why it's set here. --no-incremental: an up to date build reports nothing.
const build = spawnSync(
  "dotnet",
  ["build", project, "--no-incremental", "-p:CI=false", "-nologo", "-v", "q"],
  { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
);
if (build.status !== 0) {
  process.stdout.write(build.stdout);
  process.stderr.write(build.stderr);
  console.error("sync-public-api: the build failed, so PublicAPI.Unshipped.txt is unchanged.");
  process.exit(1);
}

const symbols = (pattern) => new Set([...build.stdout.matchAll(pattern)].map((match) => match[1]));
const added = symbols(/warning RS0016: Symbol '(.+?)' is not part of the declared public API/g);
const removed = symbols(/warning RS0017: Symbol '(.+?)' is part of the declared API/g);
if (added.size === 0 && removed.size === 0) process.exit(0);

// The file's order: case insensitive, then ordinal for lines that differ only in case.
const order = (a, b) => {
  const [lowerA, lowerB] = [a.toLowerCase(), b.toLowerCase()];
  if (lowerA !== lowerB) return lowerA < lowerB ? -1 : 1;
  return a < b ? -1 : a > b ? 1 : 0;
};
const lines = readFileSync(unshipped, "utf8")
  .split("\n")
  .filter((line) => line !== "" && line !== header && !removed.has(line));
const updated = [...new Set([...lines, ...added])].toSorted(order);
writeFileSync(unshipped, `${[header, ...updated].join("\n")}\n`);
console.log(`sync-public-api: ${added.size} added, ${removed.size} removed.`);
