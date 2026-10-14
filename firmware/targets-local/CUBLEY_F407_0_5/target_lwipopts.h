#ifndef TARGET_LWIPOPTS_H
#define TARGET_LWIPOPTS_H

// Dual-stack IPv4/IPv6 support (link-local + SLAAC + Router Discovery).
// LWIP_IPV6_AUTOCONFIG, LWIP_ICMP6, LWIP_IPV6_MLD and LWIP_IPV6_SEND_ROUTER_SOLICIT
// all default to LWIP_IPV6 in lwIP's own opt.h, so enabling LWIP_IPV6 here is
// sufficient to bring up DAD, ND6, ICMPv6, MLD and SLAAC global addressing.
// DHCPv6 is intentionally left disabled (non-goal): addresses are obtained via
// SLAAC or static configuration, DNS servers via RDNSS or static configuration.
#define LWIP_IPV6 1
#define LWIP_IPV6_DHCP6 0

// Accept DNS server addresses advertised in Router Advertisements (RFC 8106).
#define LWIP_ND6_RDNSS_MAX_DNS_SERVERS 2

#define CHECKSUM_GEN_IP 1
#define CHECKSUM_GEN_UDP 1
#define CHECKSUM_GEN_TCP 1
#define CHECKSUM_GEN_ICMP 1
#define CHECKSUM_GEN_ICMP6 1

#define CHECKSUM_CHECK_IP 1
#define CHECKSUM_CHECK_UDP 1
#define CHECKSUM_CHECK_TCP 1
#define CHECKSUM_CHECK_ICMP 1
#define CHECKSUM_CHECK_ICMP6 1

#endif // TARGET_LWIPOPTS_H