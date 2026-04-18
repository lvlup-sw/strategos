import { z } from "zod"

export const TestRefSchema = z.object({ "name": z.string(), "file": z.string() }).describe("Reference to a test file.")
export type TestRefSchema = z.infer<typeof TestRefSchema>
