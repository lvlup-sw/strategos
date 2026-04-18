import { z } from "zod"

export const WorkflowStartedDataSchema = z.object({ "featureId": z.string(), "workflowType": z.any(), "designPath": z.string().optional(), "synthesisPolicy": z.union([z.literal("always"), z.literal("never"), z.literal("on-request")]).optional() }).describe("Data payload for `workflow.started` events.")
export type WorkflowStartedDataSchema = z.infer<typeof WorkflowStartedDataSchema>
