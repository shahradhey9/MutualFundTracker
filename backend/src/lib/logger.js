import winston from 'winston';

const { combine, timestamp, printf, colorize, errors } = winston.format;

const fmt = printf(({ level, message, timestamp, stack }) =>
  `${timestamp} [${level}] ${stack || message}`
);

export const logger = winston.createLogger({
  level: process.env.NODE_ENV === 'production' ? 'info' : 'debug',
  format: combine(timestamp(), errors({ stack: true }), fmt),
  transports: [
    new winston.transports.Console({
      format: combine(colorize(), timestamp(), errors({ stack: true }), fmt),
    }),
  ],
});
