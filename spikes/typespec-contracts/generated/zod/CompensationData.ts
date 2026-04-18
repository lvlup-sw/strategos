import { z } from "zod"

export const CompensationDataSchema = z.object({ "featureId": z.string(), "actionId": z.string(), "status": z.any(), "message": z.string() }).describe("Data payload for `workflow.compensation` events.")
export type CompensationDataSchema = z.infer<typeof CompensationDataSchema>
