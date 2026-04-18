import { z } from "zod"

export const EventSourceSchema = z.enum(["exarchos","basileus"]).describe("Source system that emitted the event.")
export type EventSourceSchema = z.infer<typeof EventSourceSchema>
