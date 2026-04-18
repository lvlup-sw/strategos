import { z } from "zod"

export const TeammateInfoSchema = z.object({ "name": z.string(), "role": z.string(), "llmModel": z.string().optional() }).describe("Information about a teammate in an SDLC workflow team.")
export type TeammateInfoSchema = z.infer<typeof TeammateInfoSchema>
