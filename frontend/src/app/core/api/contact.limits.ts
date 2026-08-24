/**
 * The contact caps, mirroring the backend's ContactLimits. Kept the same so the form guides a visitor
 * to what the endpoint will accept rather than letting them write a message the server then rejects.
 */
export const ContactLimits = {
  nameMax: 100,
  emailMax: 254,
  messageMax: 5000,
} as const;
