import { z } from "zod"

export const GateExecutedDataSchema = z.object({ "gateName": z.string(), "layer": z.string(), "passed": z.boolean(), "duration": z.number().optional(), "details": z.any().optional() }).describe("Data payload for `gate.executed` events.")
export type GateExecutedDataSchema = z.infer<typeof GateExecutedDataSchema>
